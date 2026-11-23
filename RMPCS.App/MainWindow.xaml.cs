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
    public static readonly DependencyProperty CanChangeStartupProperty=DependencyProperty.Register(nameof(CanChangeStartup),typeof(bool),typeof(MainWindow),new PropertyMetadata(false));
    public bool CanChangeStartup {get=>(bool)GetValue(CanChangeStartupProperty);private set=>SetValue(CanChangeStartupProperty,value);}
    private TrayService? tray;
    private bool exitRequested, trayNoticeShown;
    internal bool TestMode { get; set; }
    internal bool SessionEnding { get; set; }
    internal void AttachTray(TrayService service) { tray = service; RefreshTrayHint(); }
    private void RefreshTrayHint()
    {
        if (tray is null) return;
        if (!tray.Available) { TrayStatus.Text = "Tray unavailable. Closing exits RMPCS when no task is active."; return; }
        TrayStatus.Text = (preferences.CloseToTray ? "Close hides to tray. " : "Close exits when idle. ") +
            (preferences.MinimizeToTray ? "Minimize hides to tray. " : "Minimize keeps the taskbar window. ") + "Use Settings to change window behavior.";
    }
    internal void TrayUnavailable() => TrayStatus.Text = "Tray unavailable. Closing will exit RMPCS.";
    internal void ShowFromTray()
    {
        Show(); WindowState = WindowState.Normal; Activate();
    }
    internal void HideToTray()
    {
        if (tray?.Available != true) return;
        Hide();
        if (!trayNoticeShown && preferences.TrayNotifications) { tray.Notify("Still running in the system tray. Click the icon to reopen, or right-click it to Exit."); trayNoticeShown = true; }
    }
    internal void RequestExit()
    {
        if (!DesktopPolicy.CanExit(busy)) { ShowFromTray(); Dialogs.Show(this, "Wait for the active task to finish. Stop after current task prevents later tasks from starting.", "Task running"); return; }
        exitRequested = true; Close();
    }
    private void Tray_Click(object sender, RoutedEventArgs e) => HideToTray();
    private void Exit_Click(object sender, RoutedEventArgs e) => RequestExit();
    private void Window_StateChanged(object? sender, EventArgs e) { if (WindowState == WindowState.Minimized && !TestMode && preferences.MinimizeToTray) HideToTray(); }
    private readonly bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public MainWindow(bool testMode = false)
    {
        TestMode = testMode;
        InitializeComponent();
        AdminStatus.Text = admin ? "Administrator session" : "Standard session";
        ElevateButton.Visibility = admin ? Visibility.Collapsed : Visibility.Visible;
        SetBusy(false);
        SourceInitialized += (_,_)=>{ThemeManager.ApplyTitleBar(this);if(!TestMode){var area=SystemParameters.WorkArea;MinWidth=Math.Min(MinWidth,area.Width);MinHeight=Math.Min(MinHeight,area.Height);Width=Math.Min(Width,area.Width);Height=Math.Min(Height,area.Height);}};
        InitializePreferences();
        Activity.Text="Your results will appear here.\n\nStart with Check my PC for a read-only health snapshot. Review maintenance tasks before choosing Run selected tasks. Help explains what each tool does.";
    }
    public void CheckAdministrator(bool showWarning = true)
    {
        if (admin) try { StartupBackupSecurity.Protect(false); } catch (Exception ex) { Log("Startup backup protection requires attention: " + ex.Message); }
        if (!admin)
        {
            Status.Text = "Administrator access required.";
            if (showWarning) Dialogs.Show(this, "Right-click RMPCS.exe and select Run as administrator, or use the Run as administrator button here. Maintenance and automation changes stay disabled until you relaunch with admin rights.", "Run RMPCS as administrator", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        busy = value;CanChangeStartup=!value&&admin;
        tray?.Status(value);
        TaskPanel.IsEnabled = PreviewButton.IsEnabled = HistoryPanel.IsEnabled = !value;
        HealthPanel.IsEnabled = StartupAppsPanel.IsEnabled = !value;
        SettingsPanel.IsEnabled = !value;
        DiagnosticsPanel.IsEnabled = ProtectionPanel.IsEnabled = !value;
        CreateRestoreButton.IsEnabled = !value && admin;
        UndoAdminButton.IsEnabled = !value && admin && TaskSchedulerService.IsInstalled;
        ApplyAdminPreference.IsEnabled = InstalledAdminChoice.IsEnabled = !value && admin && TaskSchedulerService.IsInstalled;

        RunButton.IsEnabled = AutomationPanel.IsEnabled = !value && admin;
        CareButton.IsEnabled = !value;
        AutostartSwitch.IsEnabled=ScheduleEnabledSwitch.IsEnabled=!value&&admin&&automationStatusKnown&&TaskSchedulerService.IsInstalled;
        ElevateButton.IsEnabled = !value && !admin;
        StopButton.IsEnabled = value;
        StopButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        Progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        Progress.IsIndeterminate = value;
    }
    private bool Confirm(string text) => Dialogs.Show(this, text, "RMPCS", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
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
    private void Care_Click(object sender, RoutedEventArgs e) { if(busy)return;ApplyPlan(MaintenancePlan.Care);MainTabs.SelectedIndex=1;Status.Text="Review the selected tasks, then choose Run selected tasks. Nothing has run yet."; }
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
        RunSummaryText.Clear(); SummaryExpander.Visibility = Visibility.Collapsed;
        SetBusy(true); stopRequested = false;
        try
        {
            using var lease = RunLease.Acquire();
            if (lease is null) { Status.Text = "Another maintenance session is running. Try again later."; return; }
            using var session = new SessionLog("Manual run");
            void Output(string text) { session.Write(text); Log(text); }
            Output(plan.Describe());
            if (preferences.RestoreBeforeRepair && (plan.RepairImage || plan.ScanFiles || plan.ResetSearch))
            {
                Status.Text = "Requesting and verifying a restore point...";
                var point = await RestorePoints.CreateAsync(); Output(point.Message);
                if (stopRequested || (!point.Verified && !Confirm(point.Message + "\n\nContinue these tasks WITHOUT a verified new restore point?\nSystem Restore does not recover deleted personal files.")))
                { Status.Text = "Maintenance cancelled before any task started."; Output(Status.Text); return; }
            }
            Status.Text = "Maintenance running - follow activity below.";
            var before = DriveSpaceSnapshot.Capture();
            var clock = Stopwatch.StartNew();
            var result = await MaintenanceRunner.RunAsync(plan, new WindowsOperations(Output), Output, () => stopRequested);
            clock.Stop();
            RunSummaryText.Text = RunSummary.Format(result, clock.Elapsed, before, DriveSpaceSnapshot.Capture());
            SummaryExpander.Visibility = Visibility.Visible; SummaryExpander.IsExpanded = true;
            Output(RunSummaryText.Text);
            Status.Text = result.Stopped ? "Queue stopped after current task." : result.Failed > 0 ? $"Finished with {result.Failed} failed/skipped tasks. Review activity." : "Tasks finished. Review repair output for findings.";
            if (result.RestartScheduled) Status.Text = "Restart countdown running. Save work or click Cancel restart.";
            Output($"{Status.Text} File entries skipped: {result.SkippedEntries}. No claim of repair success is made beyond the command output.");
            Log("Saved run: " + session.FilePath);
            if (session.Error is not null) Log("Log write warning: " + session.Error);
            NotifyCompletion(session.Error is null ? CompletionNotices.From(result) : CompletionNotice.NeedsReview);
        }
        catch (Exception ex) { Status.Text = "Unable to complete maintenance."; Log(ex.Message); NotifyCompletion(CompletionNotice.NeedsReview); }
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
        try { var entries=TestMode?Array.Empty<StartupItem>():StartupManager.List(Log);StartupEntries.ItemsSource = entries; StartupCommand.Text = "Select an entry to review its stored command. Commands are never launched here.";StartupListStatus.Text=(entries.GroupBy(x=>x.Id).Any(g=>g.Count()>1)?"A live entry and saved backup overlap. Review the details; conflicting values are never overwritten. ":"")+$"{entries.Count} startup entries. On means an entry is present, not that Windows guarantees it will start."+(!admin?" Run as administrator to change these switches.":""); }
        catch (Exception ex) { Log("Startup list unavailable: " + ex.Message); }
    }
    private void StartupAppsRefresh_Click(object sender, RoutedEventArgs e) => RefreshStartupApps();
    private void StartupSelection_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    { if (StartupEntries.SelectedItem is StartupItem item) StartupCommand.Text = $"{item.Name}\n{item.Scope} - {item.State}\n{item.Command}"; }
    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        if(sender is not System.Windows.Controls.CheckBox toggle || toggle.DataContext is not StartupItem item)return;
        bool requested=toggle.IsChecked==true;
        toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,item.Enabled);
        try
        {
            var outcome=StartupSwitchFlow.Change(item,requested,admin&&!busy&&!TestMode,
                ()=>Confirm($"Turn {(requested?"on":"off")} the startup entry for '{item.Name}'?\n{item.ScopeLabel}\n\n{item.Command}\n\n"+(requested?"Restore the original entry saved by RMPCS. This can allow the app to start at the next sign-in; it will not be launched now.":"A protected backup is kept so you can turn it back on. The running app will not be stopped. Turn off only entries you recognize; vendor utilities may provide hardware features.")),
                StartupManager.Disable,StartupManager.Restore);
            if(outcome==StartupSwitchResult.Changed){Log("Startup entry " +(requested?"restored: ":"disabled with backup: ")+item.Name);RefreshStartupApps();}
            else StartupListStatus.Text=outcome==StartupSwitchResult.Denied?"Administrator access is required to change startup entries.":"No startup entry was changed.";
        }
        catch(Exception ex){Log("Startup change could not be completed: "+ex.Message);RefreshStartupApps();StartupListStatus.Text="Change could not be confirmed. Review the refreshed entry and its details. "+ex.Message;}
    }
    private void ShowStartup_Click(object sender,RoutedEventArgs e)=>MainTabs.SelectedIndex=3;
    private void MainTabs_Changed(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!ReferenceEquals(e.Source,MainTabs)||RunButton is null)return;
        RunButton.Visibility=MainTabs.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;
        if(MainTabs.SelectedIndex==3&&!busy&&!TestMode)RefreshStartupApps();
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
            automationStatusKnown=false;
            var startup = TaskSchedulerService.Read(true);StartupStatus.Text = startup.Status;
            autostartEnabled=startup.Status.StartsWith("Enabled",StringComparison.Ordinal);AutostartSwitch.IsChecked=autostartEnabled;AutostartSwitch.IsEnabled=admin&&!busy&&TaskSchedulerService.IsInstalled;
            var state = TaskSchedulerService.Read(false); ScheduleStatus.Text = state.Status;
            automationStatusKnown=true;
            scheduleEnabled=state.Status.StartsWith("Enabled",StringComparison.Ordinal);ScheduleEnabledSwitch.IsChecked=scheduleEnabled;ScheduleEnabledSwitch.IsEnabled=admin&&!busy&&TaskSchedulerService.IsInstalled;
            if (state.Xml is not null)
            {
                var options = TaskSchedulerService.ParseOptions(state.Xml);
                Frequency.SelectedIndex = options.Frequency == ScheduleFrequency.Daily ? 0 : 1;
                WeekDay.SelectedIndex = (int)options.Day; ScheduleTime.Text = $"{options.Hour:00}:{options.Minute:00}";
                ScheduleProfile.SelectedIndex = options.Repair ? 1 : 0; IdleOnly.IsChecked = options.RequireIdle; AcOnly.IsChecked = options.RequireAc;
                StartWindowChoice.SelectedIndex = Array.IndexOf(new[] { 0, 60, 120, 240 }, options.StartWindowMinutes);
                WorkloadDeferChoice.SelectedIndex = Array.IndexOf(new[] { 0, 15, 30, 60 }, options.WorkloadDeferMinutes);
            }
        }
        catch (Exception ex) { StartupStatus.Text = ScheduleStatus.Text = "Status unavailable: " + ex.Message;automationStatusKnown=false;AutostartSwitch.IsEnabled=ScheduleEnabledSwitch.IsEnabled=false; }
    }
    private void ChangeAutomation(Action action)
    {
        if (!admin || busy) return;
        try { action(); Log("Automation settings updated."); } catch (Exception ex) { Dialogs.Show(this, ex.Message, "Automation not changed", MessageBoxButton.OK, MessageBoxImage.Information); }
        RefreshAutomation();
    }
    private void StartupOn_Click(object sender, RoutedEventArgs e) { if (Confirm("Start RMPCS in the system tray with administrator rights at sign-in for this Windows user? No maintenance runs just because the app opens.\n\nOnly the protected installed copy supports automatic startup. This updates any existing RMPCS sign-in task.")) ChangeAutomation(() => TaskSchedulerService.Save(true)); }
    private void StartupOff_Click(object sender, RoutedEventArgs e) => ChangeAutomation(() => TaskSchedulerService.Remove(true));
    private void SaveSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeOnly.TryParseExact(ScheduleTime.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) { Dialogs.Show(this, "Enter a 24-hour local time such as 19:00."); return; }
        var options = new ScheduleOptions(Frequency.SelectedIndex == 0 ? ScheduleFrequency.Daily : ScheduleFrequency.Weekly, (DayOfWeek)WeekDay.SelectedIndex, time.Hour, time.Minute, ScheduleProfile.SelectedIndex == 1, IdleOnly.IsChecked == true, AcOnly.IsChecked == true)
        { StartWindowMinutes = new[] { 0, 60, 120, 240 }[Math.Clamp(StartWindowChoice.SelectedIndex, 0, 3)], WorkloadDeferMinutes = new[] { 0, 15, 30, 60 }[Math.Clamp(WorkloadDeferChoice.SelectedIndex, 0, 3)] };
        if (!Confirm($"Enable {options.Frequency.ToString().ToLowerInvariant()} automatic {(options.Repair ? "cleanup and Windows repair" : "light cleanup")} at {time:HH:mm} local PC time" + (options.Frequency == ScheduleFrequency.Weekly ? $" on {options.Day}" : "") + "?\n\nThis authorizes unattended permanent deletion of eligible temp files older than 7 days and DNS flushing" + (options.Repair ? ", followed by DISM and SFC" : "") + ". You must be signed in. Advanced cleanup, network lease resets and restarts are excluded. Logs are saved locally.")) return;
        if ((options.StartWindowMinutes != 0 || options.WorkloadDeferMinutes != 0) && !Confirm($"Start window: {(options.StartWindowMinutes == 0 ? "unrestricted" : options.StartWindowMinutes + " minutes after scheduled time")}. Workload wait: {options.WorkloadDeferMinutes} minutes maximum.\n\nWhen enabled, workload checks defer for CPU >=70%, RAM >=85%, network >=5 MiB/s, Windows full-screen/presentation/quiet signals, active microphone sessions or unavailable signals. Detection is best effort. Runs are skipped when the window/wait expires. These guards never terminate a task already running.")) return;
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
        if (DesktopPolicy.HideOnClose(tray?.Available == true && preferences.CloseToTray, exitRequested, TestMode)) { e.Cancel = true; HideToTray(); return; }
        if (busy) { e.Cancel = true; Dialogs.Show(this, "Wait for the active task to finish. Stop after current task prevents later tasks from starting.", "Task running"); }
    }
}
