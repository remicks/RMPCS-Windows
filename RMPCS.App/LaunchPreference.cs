using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace RMPCS.App;

internal static class LaunchPreference
{
    public static bool RelaunchIfRequested(string[] args)
    {
        if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) || !TaskSchedulerService.IsInstalled) return false;
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var options = machine.OpenSubKey(@"SOFTWARE\DegeneratesInc\RMPCS\InstallOptions");
        if (!RMPCS.Core.InstallLaunchPolicy.RequestElevation(true, false, options?.GetValue("AlwaysAdmin") as string)) return false;
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory };
        // Only desktop activation is forwarded. Maintenance arguments are handled separately.
        if (RMPCS.Core.DesktopPolicy.StartHidden(args)) start.ArgumentList.Add("--startup");
        Process.Start(start);
        return true;
    }
}
