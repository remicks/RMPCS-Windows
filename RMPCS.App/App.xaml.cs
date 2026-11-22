using System.Security.Principal;
using System.Windows;
using RMPCS.Core;

namespace RMPCS.App;
public partial class App : Application
{
    private TrayService? tray;
    private DesktopSession? desktop;
    protected override void OnExit(ExitEventArgs e)
    { tray?.Dispose(); desktop?.Dispose(); base.OnExit(e); }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    { if (MainWindow is MainWindow window) window.SessionEnding = true; base.OnSessionEnding(e); }
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 1 && e.Args[0] == "--backup-acl-check")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { StartupBackupSecurity.VerifyDescriptor(); Shutdown(0); } catch { Shutdown(1); }
            return;
        }
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
                if (lease is null) { log.Write("Skipped: another maintenance session is active."); CompletionRelay.Send(CompletionNotice.Skipped); exit = 0; }
                else
                {
                    var plan = e.Args[0] == "--scheduled-repair" ? MaintenancePlan.Care : MaintenancePlan.Light;
                    log.Write(plan.Describe());
                    var before = DriveSpaceSnapshot.Capture();
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    var result = await MaintenanceRunner.RunAsync(plan, new WindowsOperations(log.Write), log.Write);
                    clock.Stop();
                    log.Write(RunSummary.Format(result, clock.Elapsed, before, DriveSpaceSnapshot.Capture()));
                    log.Write($"Completed: {result.Failed} failed tasks; {result.SkippedEntries} file entries skipped. Review repair command output.");
                    exit = result.Failed == 0 && log.Error is null ? 0 : 1;
                    CompletionRelay.Send(log.Error is null ? CompletionNotices.From(result) : CompletionNotice.NeedsReview);
                }
            }
            catch (Exception ex)
            {
                try { using var failure = new SessionLog("Scheduled startup failure"); failure.Write(ex.ToString()); } catch { }
                CompletionRelay.Send(CompletionNotice.NeedsReview);
            }
            Shutdown(exit); return;
        }
        bool smoke = e.Args.Contains("--smoke-test");
        bool preview = e.Args.Length is 2 or 3 && e.Args[0] == "--ui-preview";
        bool traySmoke = e.Args.Length is 1 or 2 && e.Args[0] == "--tray-smoke-test";
        bool test = smoke || preview || traySmoke;
        if (!test)
        {
            try { if (LaunchPreference.RelaunchIfRequested(e.Args)) { Shutdown(); return; } }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { /* UAC cancellation leaves a usable standard session. */ }
            catch (Exception ex) { MessageBox.Show("Administrator launch could not complete. RMPCS will open in a standard session.\n\n" + ex.Message, "RMPCS"); }
            try
            {
                desktop = new DesktopSession();
                if (!desktop.IsOwner) { Shutdown(); return; }
            }
            catch (Exception ex) { MessageBox.Show("RMPCS could not establish its desktop session: " + ex.Message, "RMPCS"); Shutdown(1); return; }
        }
        var window = new MainWindow(test); MainWindow = window;
        if (preview && e.Args.Length == 3 && int.TryParse(e.Args[2], out int tab) && tab >= 0 && tab < window.MainTabs.Items.Count) window.MainTabs.SelectedIndex = tab;
        if (!smoke && !preview)
        {
            try { tray = new TrayService(window.ShowFromTray, window.RequestExit, window.NotifyCompletion); window.AttachTray(tray); }
            catch (Exception ex) { window.TrayUnavailable(); if (traySmoke && e.Args.Length == 2) System.IO.File.WriteAllText(e.Args[1], "Tray creation failed: " + ex); }
        }
        desktop?.Listen(() => Dispatcher.BeginInvoke(window.ShowFromTray));
        window.Closed += (_, _) => Shutdown();
        if (DesktopPolicy.StartHidden(e.Args) && tray?.Available == true && !test)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            window.CheckAdministrator(false);
            window.BeginAutomaticUpdateCheck();
            return;
        }
        bool contentHandled = false;
        window.ContentRendered += (_, _) =>
        {
            if (contentHandled) return;
            contentHandled = true;
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
            else if (smoke) window.Close();
            else if (traySmoke)
            {
                void Report(string text) { if (e.Args.Length == 2) System.IO.File.WriteAllText(e.Args[1], text); }
                if (tray?.Available != true) { Report(tray?.Diagnostic ?? "Tray constructor unavailable"); Shutdown(1); return; }
                window.TestMode = false;
                window.Close(); // Exercise the real close-to-tray handler.
                if (window.IsVisible) { Report("Close did not hide window"); Shutdown(1); return; }
                window.ShowFromTray();
                window.WindowState = WindowState.Minimized;
                if (window.IsVisible) { Report("Minimize did not hide window"); Shutdown(1); return; }
                window.ShowFromTray(); tray.Status(true); tray.Status(false);
                bool passed = window.IsVisible && window.WindowState == WindowState.Normal;
                Report(passed ? "Native tray registration, close-to-tray, minimize-to-tray, restore and status checks passed. No maintenance performed." : "Window restore failed");
                Shutdown(passed ? 0 : 1);
            }
            else { window.CheckAdministrator(); window.BeginAutomaticUpdateCheck(); }
        };
        window.Show();
    }
}
