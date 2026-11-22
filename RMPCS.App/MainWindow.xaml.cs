using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Windows;
using Microsoft.Win32;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow : Window
{
    private bool busy, stopRequested;
    private TrayService? tray;
    private bool exitRequested, trayNoticeShown;
    internal bool TestMode { get; set; }
    internal bool SessionEnding { get; set; }
    internal void AttachTray(TrayService service) { tray = service; TrayStatus.Text = service.Available ? "Close or minimize to keep RMPCS in the tray." : "Tray unavailable. Closing will exit RMPCS."; }
    internal void TrayUnavailable() => TrayStatus.Text = "Tray unavailable. Closing will exit RMPCS.";
    internal void ShowFromTray()
    {
        Show(); WindowState = WindowState.Normal; Activate();
    }
    internal void HideToTray()
    {
        if (tray?.Available != true) return;
        Hide();
        if (!trayNoticeShown) { tray.Notify("Still running in the system tray. Click the icon to reopen, or right-click it to Exit."); trayNoticeShown = true; }
    }
    internal void RequestExit()
    {
        if (!DesktopPolicy.CanExit(busy)) { ShowFromTray(); MessageBox.Show(this, "Wait for the active task to finish. Stop after current task prevents later tasks from starting.", "Task running"); return; }
        exitRequested = true; Close();
    }
    private void Tray_Click(object sender, RoutedEventArgs e) => HideToTray();
    private void Exit_Click(object sender, RoutedEventArgs e) => RequestExit();
    private void Window_StateChanged(object? sender, EventArgs e) { if (WindowState == WindowState.Minimized && !TestMode) HideToTray(); }
    private readonly bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public MainWindow()
    {
        InitializeComponent();
        AdminStatus.Text = admin ? "Administrator session" : "Standard session";
        SetBusy(false);
        Log("RMPCS 0.4.0 - Written by Wade. Health scan, cleanup previews and startup management are ready.");
        Log("Select full batch to include advanced tasks and reboot, or use recommended care.");
    }
    public void CheckAdministrator(bool showWarning = true)
    {
        if (admin) try { StartupBackupSecurity.Protect(false); } catch (Exception ex) { Log("Startup backup protection requires attention: " + ex.Message); }
        if (!admin)
        {
            Status.Text = "Administrator access required.";
            if (showWarning) MessageBox.Show(this, "Right-click RMPCS.exe and select Run as administrator, or use the Run as administrator button here. Maintenance and automation changes stay disabled until you relaunch with admin rights.", "Run RMPCS as administrator", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshAutomation();
        RefreshHistory();
    }
    private void Log(string text)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => Log(text)); return; }
        if (Activity.Text.Length > 180_000) Activity.Text = "[Earlier activity trimmed; full run is in History]\n" + Activity.Text[^120_000..];
        Activity.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        Activity.ScrollToEnd();
    }
    private void SetBusy(bool value)
    {
        busy = value;
        tray?.Status(value);
        TaskPanel.IsEnabled = PreviewButton.IsEnabled = HistoryPanel.IsEnabled = !value;
        HealthPanel.IsEnabled = StartupAppsPanel.IsEnabled = !value;
        DisableStartupApp.IsEnabled = RestoreStartupApp.IsEnabled = !value && admin;
        RunButton.IsEnabled = CareButton.IsEnabled = AutomationPanel.IsEnabled = !value && admin;
        ElevateButton.IsEnabled = !value && !admin;
        StopButton.IsEnabled = value;
        Progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        Progress.IsIndeterminate = value;
    }
    private bool Confirm(string text) => MessageBox.Show(this, text, "RMPCS", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    private MaintenancePlan SelectedPlan() => new()
    {
        UserTemp = UserTemp.IsChecked == true, WindowsTemp = SystemTemp.IsChecked == true,
        Prefetch = Prefetch.IsChecked == true, WindowsLogs = WindowsLogs.IsChecked == true,
        ResetSearch = SearchIndex.IsChecked == true, FlushDns = Dns.IsChecked == true,
        Thumbnails = Thumbnails.IsChecked == true, ErrorArchives = ErrorArchives.IsChecked == true,
        CrashDumps = CrashDumps.IsChecked == true, RecycleBin = RecycleBin.IsChecked == true,
        RepairImage = Dism.IsChecked == true, ScanFiles = Sfc.IsChecked == true,
        RenewDhcp = Dhcp.IsChecked == true, Restart = RebootAfter.IsChecked == true,
        MinimumAgeDays = Age.SelectedIndex switch { 0 => 0, 1 => 1, 3 => 30, _ => 7 },
        RestartSeconds = RebootDelay.SelectedIndex switch { 0 => 10, 2 => 300, _ => 60 }
    };
    private void ApplyPlan(MaintenancePlan plan)
    {
        UserTemp.IsChecked = plan.UserTemp; SystemTemp.IsChecked = plan.WindowsTemp;
        Prefetch.IsChecked = plan.Prefetch; WindowsLogs.IsChecked = plan.WindowsLogs; SearchIndex.IsChecked = plan.ResetSearch;
        Thumbnails.IsChecked = plan.Thumbnails; ErrorArchives.IsChecked = plan.ErrorArchives;
        CrashDumps.IsChecked = plan.CrashDumps; RecycleBin.IsChecked = plan.RecycleBin;
        Dns.IsChecked = plan.FlushDns; Dism.IsChecked = plan.RepairImage; Sfc.IsChecked = plan.ScanFiles;
        Dhcp.IsChecked = plan.RenewDhcp; RebootAfter.IsChecked = plan.Restart;
        Age.SelectedIndex = plan.MinimumAgeDays switch { 0 => 0, 1 => 1, 30 => 3, _ => 2 };
        RebootDelay.SelectedIndex = plan.RestartSeconds switch { 10 => 0, 300 => 2, _ => 1 };
    }
    private void SelectCare_Click(object sender, RoutedEventArgs e) => ApplyPlan(MaintenancePlan.Care);
    private void SelectBatch_Click(object sender, RoutedEventArgs e) { ApplyPlan(MaintenancePlan.FullBatch); AdvancedOptions.IsExpanded = true; Status.Text = "Full batch selected. Review advanced tasks and reboot before running."; }
    private async void Care_Click(object sender, RoutedEventArgs e) { ApplyPlan(MaintenancePlan.Care); await StartPlanAsync(SelectedPlan()); }
    private async void Run_Click(object sender, RoutedEventArgs e) => await StartPlanAsync(SelectedPlan());
    private async Task StartPlanAsync(MaintenancePlan plan)
    {
        if (!admin || busy) return;
        if (plan.IsEmpty) { Status.Text = "Select at least one task."; return; }
        string notice = "Run these tasks?\n\n" + plan.Describe() + "\n\nFile deletion is permanent. Locked/protected files and links are skipped.";
        if (plan.Prefetch) notice += "\nPrefetch removal may temporarily slow application launches.";
        if (plan.WindowsLogs) notice += "\nWindows Logs cleanup removes troubleshooting history.";
        if (plan.ErrorArchives || plan.CrashDumps) notice += "\nSelected diagnostic reports/dumps will be permanently removed and may be needed to investigate crashes.";
        if (plan.RecycleBin) notice += "\nALL items currently in this Windows identity's Recycle Bins will be permanently removed, regardless of age.";
        if (plan.ResetSearch) notice += "\nSearch results may be incomplete during the background rebuild.";
        if (plan.RenewDhcp) notice += "\nDHCP release disconnects network/VPN/remote sessions; reconnection is not guaranteed.";
        if (plan.Restart) notice += "\nSAVE ALL WORK. Windows may force applications to close at the end of the restart countdown.";
        if (!Confirm(notice)) return;
        SetBusy(true); stopRequested = false;
        try
        {
            using var lease = RunLease.Acquire();
            if (lease is null) { Status.Text = "Another maintenance session is running. Try again later."; return; }
            using var session = new SessionLog("Manual run");
            void Output(string text) { session.Write(text); Log(text); }
            Output(plan.Describe());
            Status.Text = "Maintenance running - follow activity below.";
            var result = await MaintenanceRunner.RunAsync(plan, new WindowsOperations(Output), Output, () => stopRequested);
            Status.Text = result.Stopped ? "Queue stopped after current task." : result.Failed > 0 ? $"Finished with {result.Failed} failed/skipped tasks. Review activity." : "Tasks finished. Review repair output for findings.";
            if (result.RestartScheduled) Status.Text = "Restart countdown running. Save work or click Cancel restart.";
            Output($"{Status.Text} File entries skipped: {result.SkippedEntries}. No claim of repair success is made beyond the command output.");
            Log("Saved run: " + session.FilePath);
            if (session.Error is not null) Log("Log write warning: " + session.Error);
        }
        catch (Exception ex) { Status.Text = "Unable to complete maintenance."; Log(ex.Message); }
        finally { SetBusy(false); RefreshHistory(); }
    }
    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var plan = SelectedPlan();
        if (!plan.CleanupAreas().Any()) { Status.Text = "Select a file cleanup task first."; return; }
        stopRequested = false; SetBusy(true);
        PreviewRows.Visibility = Visibility.Visible;
        try
        {
            long bytes = 0; int count = 0;
            var rows = new List<CleanupPreviewRow>();
            foreach (var area in plan.CleanupAreas())
            {
                if (stopRequested) break;
                var preview = await Task.Run(() => CleanupPreviewService.Scan(area, plan.MinimumAgeDays));
                rows.Add(preview); PreviewRows.ItemsSource = rows.ToArray();
                bytes += preview.Bytes; count += (int)Math.Min(int.MaxValue - count, preview.Files);
                Log($"Preview {area}: {preview.Files:N0} eligible entries; {preview.Bytes:N0} bytes; {preview.Skipped} skipped entries. {preview.Note}");
                foreach (var path in preview.Paths.Take(60)) Log(path);
                if (preview.Paths.Count > 60) Log("First 60 paths shown. Running rescans the selected roots.");
            }
            Status.Text = $"Preview only: {count:N0} files, {bytes / 1048576.0:N2} MB. Nothing deleted.";
        }
        catch (Exception ex) { Status.Text = "Preview failed."; Log(ex.Message); }
        finally { stopRequested = false; SetBusy(false); }
    }
    private async void HealthScan_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return; SetBusy(true); Status.Text = "Reading health indicators...";
        try { Activity.Text = await HealthScanner.ScanAsync(); Status.Text = "Health snapshot complete. No settings changed."; }
        catch (Exception ex) { Log("Health scan could not finish: " + ex.Message); }
        finally { SetBusy(false); }
    }
    private void RefreshStartupApps()
    {
        try { StartupEntries.ItemsSource = StartupManager.List(Log); StartupCommand.Text = "Select an entry to review its stored command."; }
        catch (Exception ex) { Log("Startup list unavailable: " + ex.Message); }
    }
    private void StartupAppsRefresh_Click(object sender, RoutedEventArgs e) => RefreshStartupApps();
    private void StartupSelection_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    { if (StartupEntries.SelectedItem is StartupItem item) StartupCommand.Text = $"{item.Name}\n{item.Scope} - {item.State}\n{item.Command}"; }
    private void DisableStartupApp_Click(object sender, RoutedEventArgs e)
    {
        if (!admin || busy || StartupEntries.SelectedItem is not StartupItem { Enabled: true } item) return;
        if (!Confirm($"Disable the Run entry '{item.Name}' ({item.Scope})?\n\n{item.Command}\n\nA protected backup is kept so you can restore it. The currently running program is not stopped. Disabling vendor utilities may affect their features at your next sign-in.")) return;
        try { StartupManager.Disable(item); Log("Startup entry disabled with backup: " + item.Name); } catch (Exception ex) { Log("Startup change failed: " + ex.Message); }
        RefreshStartupApps();
    }
    private void RestoreStartupApp_Click(object sender, RoutedEventArgs e)
    {
        if (!admin || busy || StartupEntries.SelectedItem is not StartupItem { Enabled: false } item) return;
        if (!Confirm($"Restore the original Run entry '{item.Name}' ({item.Scope})?\n\n{item.Command}\n\nThis can allow it to start at sign-in, subject to Windows/app approvals. It will not be launched now.")) return;
        try { StartupManager.Restore(item); Log("Startup entry restored: " + item.Name); } catch (Exception ex) { Log("Startup restore failed: " + ex.Message); }
        RefreshStartupApps();
    }
    private void Stop_Click(object sender, RoutedEventArgs e) { stopRequested = true; StopButton.IsEnabled = false; Log("Stopping after current task; DHCP renewal is still attempted after release."); }
    private void Elevate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the executable.");
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Start RMPCS.exe directly to request admin rights.");
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory }); RequestExit();
        }
        catch (Exception ex) { Log("Administrator relaunch did not complete: " + ex.Message); }
    }
    private void RefreshAutomation()
    {
        try
        {
            StartupStatus.Text = TaskSchedulerService.Read(true).Status;
            var state = TaskSchedulerService.Read(false); ScheduleStatus.Text = state.Status;
            if (state.Xml is not null)
            {
                var options = TaskSchedulerService.ParseOptions(state.Xml);
                Frequency.SelectedIndex = options.Frequency == ScheduleFrequency.Daily ? 0 : 1;
                WeekDay.SelectedIndex = (int)options.Day; ScheduleTime.Text = $"{options.Hour:00}:{options.Minute:00}";
                ScheduleProfile.SelectedIndex = options.Repair ? 1 : 0; IdleOnly.IsChecked = options.RequireIdle; AcOnly.IsChecked = options.RequireAc;
            }
        }
        catch (Exception ex) { StartupStatus.Text = ScheduleStatus.Text = "Status unavailable: " + ex.Message; }
    }
    private void ChangeAutomation(Action action)
    {
        if (!admin || busy) return;
        try { action(); Log("Automation settings updated."); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Automation not changed", MessageBoxButton.OK, MessageBoxImage.Information); }
        RefreshAutomation();
    }
    private void StartupOn_Click(object sender, RoutedEventArgs e) { if (Confirm("Start RMPCS in the system tray with administrator rights at sign-in for this Windows user? No maintenance runs just because the app opens.\n\nOnly the protected installed copy supports automatic startup. This updates any existing RMPCS sign-in task.")) ChangeAutomation(() => TaskSchedulerService.Save(true)); }
    private void StartupOff_Click(object sender, RoutedEventArgs e) => ChangeAutomation(() => TaskSchedulerService.Remove(true));
    private void SaveSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeOnly.TryParseExact(ScheduleTime.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) { MessageBox.Show(this, "Enter a 24-hour local time such as 19:00."); return; }
        var options = new ScheduleOptions(Frequency.SelectedIndex == 0 ? ScheduleFrequency.Daily : ScheduleFrequency.Weekly, (DayOfWeek)WeekDay.SelectedIndex, time.Hour, time.Minute, ScheduleProfile.SelectedIndex == 1, IdleOnly.IsChecked == true, AcOnly.IsChecked == true);
        if (!Confirm($"Enable {options.Frequency.ToString().ToLowerInvariant()} automatic {(options.Repair ? "cleanup and Windows repair" : "light cleanup")} at {time:HH:mm} local PC time" + (options.Frequency == ScheduleFrequency.Weekly ? $" on {options.Day}" : "") + "?\n\nThis authorizes unattended permanent deletion of eligible temp files older than 7 days and DNS flushing" + (options.Repair ? ", followed by DISM and SFC" : "") + ". You must be signed in. Advanced cleanup, network lease resets and restarts are excluded. Logs are saved locally.")) return;
        ChangeAutomation(() => TaskSchedulerService.Save(false, options));
    }
    private void RemoveSchedule_Click(object sender, RoutedEventArgs e) => ChangeAutomation(() => TaskSchedulerService.Remove(false));
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshAutomation();
    private void RefreshHistory()
    {
        try { HistoryFiles.ItemsSource = SessionLog.List().Select(p => new FileInfo(p)).ToArray(); if (HistoryFiles.Items.Count > 0) HistoryFiles.SelectedIndex = 0; }
        catch (Exception ex) { Log("History unavailable: " + ex.Message); }
    }
    private void HistoryRefresh_Click(object sender, RoutedEventArgs e) => RefreshHistory();
    private void LoadHistory_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryFiles.SelectedItem is not FileInfo file) return;
        try { Activity.Text = SessionLog.Read(file.FullName); Status.Text = "Saved run: " + file.Name; } catch (Exception ex) { Log(ex.Message); }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = $"RMPCS-{DateTime.Now:yyyyMMdd-HHmmss}.txt", Filter = "Text log (*.txt)|*.txt" };
        if (dialog.ShowDialog(this) == true) try { File.WriteAllText(dialog.FileName, Activity.Text); Log("Activity exported."); } catch (Exception ex) { Log("Export failed: " + ex.Message); }
    }
    private async void AbortRestart_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm("Cancel the PC's pending Windows restart/shutdown? This can also cancel one scheduled by another application.")) return;
        AbortRestartButton.IsEnabled = false;
        try { int exit = await new WindowsOperations(Log).RunAsync(SystemCommand.AbortRestart); Log(exit == 0 ? "Pending restart cancelled." : "No restart cancelled; review exit code."); }
        catch (Exception ex) { Log(ex.Message); }
        finally { AbortRestartButton.IsEnabled = true; }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (SessionEnding) return;
        if (DesktopPolicy.HideOnClose(tray?.Available == true, exitRequested, TestMode)) { e.Cancel = true; HideToTray(); return; }
        if (busy) { e.Cancel = true; MessageBox.Show(this, "Wait for the active task to finish. Stop after current task prevents later tasks from starting.", "Task running"); }
    }
}
