using Microsoft.Win32;
using RMPCS.Core;

namespace RMPCS.App;

internal static class SettingsStore
{
    private const string UserKey = @"Software\DegeneratesInc\RMPCS\Preferences";
    internal const string InstallKey = @"SOFTWARE\DegeneratesInc\RMPCS\InstallOptions";
    public static AppPreferences Load(out string? warning)
    {
        warning = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UserKey);
            return key?.GetValue("PreferencesJson") is string json ? AppPreferences.Parse(json) : new();
        }
        catch { warning = "Saved preferences could not be read; interface defaults are in use. Save in Settings to replace them."; return new(); }
    }
    public static void Save(AppPreferences preferences)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UserKey, true);
        AppPreferences before; try { before = key.GetValue("PreferencesJson") is string json ? AppPreferences.Parse(json) : new(); } catch { before = new(); }
        var after = preferences.Normalize();
        if (before == after) return;
        key.SetValue("InterfaceUndo", new PreferenceUndo(before, after).ToJson(), RegistryValueKind.String);
        key.SetValue("PreferencesJson", after.ToJson(), RegistryValueKind.String);
    }
    public static AppPreferences UndoInterface()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UserKey, true) ?? throw new InvalidOperationException("No interface undo record.");
        var undo = PreferenceUndo.Parse(key.GetValue("InterfaceUndo") as string ?? throw new InvalidOperationException("No interface undo record."));
        var current = AppPreferences.Parse(key.GetValue("PreferencesJson") as string ?? "{}");
        var restored = undo.Restore(current); key.SetValue("PreferencesJson", restored.ToJson(), RegistryValueKind.String);
        try { key.DeleteValue("InterfaceUndo", false); } catch { } return restored;
    }
    public static bool TryClaimAutomaticCheck()
    {
        try
        {
            string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
            using var mutex = new Mutex(false, @"Global\DegeneratesInc.RMPCS.UpdateCheck." + sid);
            bool owned = false;
            try
            {
                try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                if (!owned) return false;
                using var key = Registry.CurrentUser.CreateSubKey(UserKey, true);
                long now = DateTime.UtcNow.Ticks;
                if (key.GetValue("LastAutomaticCheckUtc") is long ticks && ticks >= 0 && ticks <= now && now - ticks < TimeSpan.FromHours(24).Ticks) return false;
                key.SetValue("LastAutomaticCheckUtc", now, RegistryValueKind.QWord);
                return true;
            }
            finally { if (owned) mutex.ReleaseMutex(); }
        }
        catch { return false; } // Manual checking remains available when storage/coordination is unavailable.
    }
    public static bool AlwaysAdmin()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(InstallKey);
        return key?.GetValue("AlwaysAdmin") as string == "1";
    }
    public static void SetAlwaysAdmin(bool enabled)
    {
        if (!TaskSchedulerService.IsInstalled) throw new InvalidOperationException("This preference applies only to the protected installed copy.");
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(InstallKey, true) ?? throw new InvalidOperationException("Installation settings are missing. Repair the installation.");
        string current = key.GetValue("AlwaysAdmin") as string ?? throw new InvalidOperationException("Installation preference is missing.");
        if (current is not ("0" or "1")) throw new InvalidOperationException("Installation preference is invalid.");
        if (current == (enabled ? "1" : "0")) return;
        key.SetValue("AdminPreferenceUndo", System.Text.Json.JsonSerializer.Serialize(new AdminUndo(current == "1", enabled, System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value)), RegistryValueKind.String);
        key.SetValue("AlwaysAdmin", enabled ? "1" : "0", RegistryValueKind.String);
    }
    private sealed record AdminUndo(bool Before, bool After, string Owner);
    public static bool UndoAdministrator()
    {
        if (!TaskSchedulerService.IsInstalled) throw new InvalidOperationException("Use the protected installed copy.");
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(InstallKey, true) ?? throw new InvalidOperationException("Installation settings unavailable.");
        string json = key.GetValue("AdminPreferenceUndo") as string ?? throw new InvalidOperationException("No administrator preference undo record.");
        if (json.Length > 1024) throw new InvalidOperationException("Invalid undo record.");
        var undo = System.Text.Json.JsonSerializer.Deserialize<AdminUndo>(json) ?? throw new InvalidOperationException("Invalid undo record.");
        if (undo.Owner != System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value || key.GetValue("AlwaysAdmin") as string != (undo.After ? "1" : "0")) throw new InvalidOperationException("Undo owner/state conflict. The current preference was preserved.");
        key.SetValue("AlwaysAdmin", undo.Before ? "1" : "0", RegistryValueKind.String);
        try { key.DeleteValue("AdminPreferenceUndo", false); } catch { } return undo.Before;
    }
}
