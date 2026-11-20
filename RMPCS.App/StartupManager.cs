using System.Security.Principal;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using RMPCS.Core;

namespace RMPCS.App;

internal static class StartupManager
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string BackupsKey = @"SOFTWARE\DegeneratesInc\RMPCS\DisabledStartup";
    private static string Sid => WindowsIdentity.GetCurrent().User!.Value;
    private static RegistryKey Base(string scope) => RegistryKey.OpenBaseKey(scope == "User64" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, scope == "Machine32" ? RegistryView.Registry32 : RegistryView.Registry64);
    private static RegistryKey BackupBase() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
    private static void RequireAdmin()
    { if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new UnauthorizedAccessException("Administrator rights are required for startup changes."); }
    public static IReadOnlyList<StartupItem> List(Action<string> warning)
    {
        var items = new List<StartupItem>();
        foreach (var scope in new[] { "User64", "Machine64", "Machine32" })
        {
            try
            {
                using var registry = Base(scope); using var key = registry.OpenSubKey(RunKey);
                if (key is null) continue;
                foreach (var name in key.GetValueNames())
                {
                    if (name.Length == 0) continue;
                    var kind = key.GetValueKind(name);
                    if (kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString)) continue;
                    if (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string command) continue;
                    var owner = scope == "User64" ? Sid : "machine";
                    items.Add(new(StartupPolicy.Id(scope, name, owner), name, command, scope, true, kind == RegistryValueKind.ExpandString, owner));
                }
            }
            catch (Exception ex) { warning(scope + ": " + ex.Message); }
        }
        try
        {
            using var registry = BackupBase(); using var backups = registry.OpenSubKey(BackupsKey);
            if (backups is not null) foreach (var name in backups.GetValueNames())
            {
                try
                {
                    var item = JsonSerializer.Deserialize<StartupItem>((string)backups.GetValue(name)!) ?? throw new InvalidDataException("Empty startup backup.");
                    if (item.Scope == "User64" && item.OwnerSid != Sid) continue;
                    StartupPolicy.Validate(item, Sid);
                    if (name != item.Id || item.Enabled) throw new InvalidDataException("Invalid startup backup identity.");
                    // A crash after backup but before deletion leaves the live entry present.
                    // Keep both visible so restoration never overwrites a changed live value.
                    items.Add(item);
                }
                catch (Exception ex) { warning("Startup backup skipped: " + ex.Message); }
            }
        }
        catch (Exception ex) { warning("Startup backups unavailable: " + ex.Message); }
        return items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Scope).ToArray();
    }
    public static void Disable(StartupItem snapshot)
    {
        RequireAdmin(); StartupPolicy.Validate(snapshot, Sid);
        if (!snapshot.Enabled) throw new InvalidOperationException("Entry is already disabled.");
        using var registry = Base(snapshot.Scope); using var key = registry.OpenSubKey(RunKey, true) ?? throw new InvalidOperationException("Startup key no longer exists.");
        var expectedKind = snapshot.Expand ? RegistryValueKind.ExpandString : RegistryValueKind.String;
        void ValidateCurrent()
        {
            if (key.GetValueKind(snapshot.Name) != expectedKind || key.GetValue(snapshot.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string != snapshot.Command)
                throw new InvalidOperationException("This entry changed since the list was loaded. Refresh before making changes.");
        }
        ValidateCurrent();
        using var backupRegistry = BackupBase(); using var backups = backupRegistry.CreateSubKey(BackupsKey, true);
        if (backups.GetValue(snapshot.Id) is not null) throw new InvalidOperationException("A backup already exists. Resolve/restore that backup before disabling again.");
        backups.SetValue(snapshot.Id, JsonSerializer.Serialize(snapshot with { Enabled = false }), RegistryValueKind.String); backups.Flush();
        ValidateCurrent(); key.DeleteValue(snapshot.Name, true); key.Flush();
    }
    public static void Restore(StartupItem snapshot)
    {
        RequireAdmin(); StartupPolicy.Validate(snapshot, Sid);
        using var backupRegistry = BackupBase(); using var backups = backupRegistry.OpenSubKey(BackupsKey, true) ?? throw new InvalidOperationException("Backup no longer exists.");
        var saved = JsonSerializer.Deserialize<StartupItem>((string?)backups.GetValue(snapshot.Id) ?? "null") ?? throw new InvalidOperationException("Backup no longer exists.");
        StartupPolicy.Validate(saved, Sid);
        if (saved != snapshot || saved.Enabled) throw new InvalidOperationException("Backup changed. Refresh the startup list.");
        using var registry = Base(saved.Scope); using var key = registry.CreateSubKey(RunKey, true);
        var existing = key.GetValue(saved.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (existing is not null)
        {
            if (existing is string text && text == saved.Command && key.GetValueKind(saved.Name) == (saved.Expand ? RegistryValueKind.ExpandString : RegistryValueKind.String))
            { backups.DeleteValue(saved.Id, true); return; }
            throw new InvalidOperationException("A live entry with this name exists. RMPCS will not overwrite it.");
        }
        key.SetValue(saved.Name, saved.Command, saved.Expand ? RegistryValueKind.ExpandString : RegistryValueKind.String); key.Flush();
        backups.DeleteValue(saved.Id, true); backups.Flush();
    }
}
