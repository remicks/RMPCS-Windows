using System.Security.Principal;
using System.Windows;
using RMPCS.Core;

namespace RMPCS.App;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 1 && e.Args[0] is "--scheduled-light" or "--scheduled-repair")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int exit = 1;
            try
            {
                if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException("Scheduled care requires administrator rights.");
                if (!TaskSchedulerService.IsInstalled) throw new InvalidOperationException("Scheduled care only runs from the protected installed copy.");
                using var log = new SessionLog(e.Args[0]);
                using var lease = RunLease.Acquire();
                if (lease is null) { log.Write("Skipped: another maintenance session is active."); exit = 0; }
                else
                {
                    var plan = e.Args[0] == "--scheduled-repair" ? MaintenancePlan.Care : MaintenancePlan.Light;
                    log.Write(plan.Describe());
                    var result = await MaintenanceRunner.RunAsync(plan, new WindowsOperations(log.Write), log.Write);
                    log.Write($"Completed: {result.Failed} failed tasks; {result.SkippedEntries} file entries skipped. Review repair command output.");
                    exit = result.Failed == 0 && log.Error is null ? 0 : 1;
                }
            }
            catch (Exception ex)
            {
                try { using var failure = new SessionLog("Scheduled startup failure"); failure.Write(ex.ToString()); } catch { }
            }
            Shutdown(exit); return;
        }
        var window = new MainWindow(); MainWindow = window;
        bool smoke = e.Args.Contains("--smoke-test");
        window.ContentRendered += (_, _) => { if (smoke) window.Close(); else window.CheckAdministrator(); };
        window.Show();
    }
}
