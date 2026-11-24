using System.Security.Principal;
using System.Windows;
using RMPCS.Core;

namespace RMPCS.App;
public partial class App : Application
{
    private TrayService? tray;
    private DesktopSession? desktop;
    protected override void OnExit(ExitEventArgs e)
    { ThemeManager.Dispose(); tray?.Dispose(); desktop?.Dispose(); base.OnExit(e); }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    { if (MainWindow is MainWindow window) window.SessionEnding = true; base.OnSessionEnding(e); }
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--diagnostic-read-check")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                string report = await DiagnosticsService.StabilityAsync(7);
                var sample = await WorkloadProbe.SampleAsync().WaitAsync(TimeSpan.FromSeconds(10));
                // Persist only validation status, never the underlying event/resource details.
                System.IO.File.WriteAllText(e.Args[1], $"Stability metadata read completed ({report.Length} characters; report not saved). Workload probe completed. Decision: {WorkloadRules.DeferReason(sample) ?? "signals allowed a start"}. No network tests, restore creation, preferences or maintenance were invoked.\n");
                Shutdown(0);
            }
            catch { System.IO.File.WriteAllText(e.Args[1], "Read-only diagnostic check failed."); Shutdown(1); }
            return;
        }
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
        if (e.Args.Length > 0 && e.Args[0].StartsWith("--scheduled-", StringComparison.Ordinal))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int exit = 1;
            try
            {
                if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException("Scheduled care requires administrator rights.");
                if (!TaskSchedulerService.IsInstalled) throw new InvalidOperationException("Scheduled care only runs from the protected installed copy.");
                if (!ScheduleLaunch.TryParse(e.Args, out var launch)) throw new InvalidOperationException("Invalid scheduled launch arguments.");
                using var log = new SessionLog(e.Args[0]);
                if (!await ScheduledStartGuard.MayStartAsync(launch, log.Write)) { CompletionRelay.Send(log.Error is null ? CompletionNotice.Deferred : CompletionNotice.NeedsReview); Shutdown(log.Error is null ? 0 : 1); return; }
                using var lease = RunLease.Acquire();
                if (lease is null) { log.Write("Skipped: another maintenance session is active."); CompletionRelay.Send(CompletionNotice.Skipped); exit = 0; }
                else
                {
                    var plan = launch.Repair ? MaintenancePlan.Care : MaintenancePlan.Light;
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
        ThemeManager.Initialize();
        bool smoke = e.Args.Contains("--smoke-test");
        bool preview = e.Args.Length is >= 2 and <= 7 && e.Args[0] == "--ui-preview";
        bool traySmoke = e.Args.Length is 1 or 2 && e.Args[0] == "--tray-smoke-test";
        bool themeCheck = e.Args.Length==2 && e.Args[0]=="--theme-ui-check";
        bool sidebarCheck = e.Args.Length == 2 && e.Args[0] == "--sidebar-ui-check";
        bool presentationCheck = e.Args.Length == 2 && e.Args[0] == "--presentation-ui-check";
        bool earlyDialogVerified=false;
        if(themeCheck)
        {
            try {earlyDialogVerified=Dialogs.VerifyEarlyDialog();if(!earlyDialogVerified)throw new InvalidOperationException("Startup dialog state was not restored.");}
            catch(Exception ex){System.IO.File.WriteAllText(e.Args[1],"Startup dialog check failed: "+ex.Message);Shutdown(1);return;}
        }
        bool test = smoke || preview || traySmoke || themeCheck || sidebarCheck || presentationCheck;
        if (!test)
        {
            try { if (LaunchPreference.RelaunchIfRequested(e.Args)) { Shutdown(); return; } }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { /* UAC cancellation leaves a usable standard session. */ }
            catch (Exception ex) { Dialogs.Show("Administrator launch could not complete. RMPCS will open in a standard session.\n\n" + ex.Message, "RMPCS"); }
            try
            {
                desktop = new DesktopSession();
                if (!desktop.IsOwner) { Shutdown(); return; }
            }
            catch (Exception ex) { Dialogs.Show("RMPCS could not establish its desktop session: " + ex.Message, "RMPCS"); Shutdown(1); return; }
        }
        var window = new MainWindow(test); MainWindow = window;
        if (preview && e.Args.Length >= 3 && int.TryParse(e.Args[2], out int tab) && tab >= 0 && tab < window.MainTabs.Items.Count) window.MainTabs.SelectedIndex = tab;
        if(preview)window.PreviewAppearance(e.Args.Length>=4?e.Args[3]:"Light",e.Args.Length>=5?e.Args[4]:"Crimson Red",e.Args.Length>=6&&e.Args[5]=="compact",e.Args.Length>=6&&e.Args[5]=="sidebar",e.Args.Length==7?e.Args[6]:"Barlow");
        if (!smoke && !preview && !themeCheck && !sidebarCheck && !presentationCheck)
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
        window.ContentRendered += async (_, _) =>
        {
            if (contentHandled) return;
            contentHandled = true;
            if (presentationCheck)
            {
                try { window.VerifyPresentationUi(e.Args[1]); window.Close(); }
                catch (Exception ex) { System.IO.File.WriteAllText(e.Args[1], "Presentation UI check failed: " + ex.Message); Shutdown(1); }
            }
            else if (sidebarCheck)
            {
                try { await window.VerifySidebarUiAsync(e.Args[1]); window.Close(); }
                catch (Exception ex) { System.IO.File.WriteAllText(e.Args[1], "Sidebar UI check failed: " + ex.Message); Shutdown(1); }
            }
            else if(themeCheck)
            {
                try { window.VerifyThemeUi(e.Args[1],earlyDialogVerified);window.Close(); }
                catch(Exception ex) {System.IO.File.WriteAllText(e.Args[1],"Theme UI check failed: "+ex.Message);Shutdown(1);}
            }
            else if (preview)
            {
                try
                {
                    if (e.Args.Length >= 6 && e.Args[5] == "sidebar") { await Task.Delay(250); window.UpdateLayout(); }
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
