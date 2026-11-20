using System.Security.Principal;
using System.Windows;
using RMPCS.Core;

namespace RMPCS.App;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--read-only-check")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                string report = await HealthScanner.ScanAsync();
                var warnings = new List<string>();
                var entries = StartupManager.List(warnings.Add);
                System.IO.File.WriteAllText(e.Args[1], $"Health scan completed without maintenance. Report characters: {report.Length}. Startup inventory read completed; no entries changed. Read warnings: {warnings.Count}.\n");
                Shutdown(0);
            }
            catch (Exception ex) { System.IO.File.WriteAllText(e.Args[1], "Read-only check failed: " + ex.Message); Shutdown(1); }
            return;
        }
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
        bool preview = e.Args.Length == 2 && e.Args[0] == "--ui-preview";
        window.ContentRendered += (_, _) =>
        {
            if (preview)
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var stream = new System.IO.FileStream(e.Args[1], System.IO.FileMode.CreateNew); encoder.Save(stream);
                }
                catch { Shutdown(1); return; }
                window.Close();
            }
            else if (smoke) window.Close(); else window.CheckAdministrator();
        };
        window.Show();
    }
}
