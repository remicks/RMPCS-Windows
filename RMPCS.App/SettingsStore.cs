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
    { using var key = Registry.CurrentUser.CreateSubKey(UserKey, true); key.SetValue("PreferencesJson", preferences.ToJson(), RegistryValueKind.String); }
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
        key.SetValue("AlwaysAdmin", enabled ? "1" : "0", RegistryValueKind.String);
    }
}
