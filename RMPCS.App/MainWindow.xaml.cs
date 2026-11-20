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
    private readonly bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public MainWindow()
    {
        InitializeComponent();
        AdminStatus.Text = admin ? "Administrator session" : "Standard session";
        SetBusy(false);
        Log("RMPCS 0.2 - Written by Wade. All WMPCS 0.3b task categories are available.");
        Log("Select full batch to include advanced tasks and reboot, or use recommended care.");
    }
    public void CheckAdministrator()
    {
        if (!admin)
        {
            Status.Text = "Administrator access required.";
            MessageBox.Show(this, "Right-click RMPCS.exe and select Run as administrator, or use the Run as administrator button here. Maintenance and automation changes stay disabled until you relaunch with admin rights.", "Run RMPCS as administrator", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        TaskPanel.IsEnabled = PreviewButton.IsEnabled = HistoryPanel.IsEnabled = !value;
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
        RepairImage = Dism.IsChecked == true, ScanFiles = Sfc.IsChecked == true,
        RenewDhcp = Dhcp.IsChecked == true, Restart = RebootAfter.IsChecked == true,
        MinimumAgeDays = Age.SelectedIndex switch { 0 => 0, 1 => 1, 3 => 30, _ => 7 },
        RestartSeconds = RebootDelay.SelectedIndex switch { 0 => 10, 2 => 300, _ => 60 }
    };
    private void ApplyPlan(MaintenancePlan plan)
    {
        UserTemp.IsChecked = plan.UserTemp; SystemTemp.IsChecked = plan.WindowsTemp;
        Prefetch.IsChecked = plan.Prefetch; WindowsLogs.IsChecked = plan.WindowsLogs; SearchIndex.IsChecked = plan.ResetSearch;
        Dns.IsChecked = plan.FlushDns; Dism.IsChecked = plan.RepairImage; Sfc.IsChecked = plan.ScanFiles;
        Dhcp.IsChecked = plan.RenewDhcp; RebootAfter.IsChecked = plan.Restart;
        Age.SelectedIndex = plan.MinimumAgeDays switch { 0 => 0, 1 => 1, 30 => 3, _ => 2 };
        RebootDelay.SelectedIndex = plan.RestartSeconds switch { 10 => 0, 300 => 2, _ => 1 };
    }
    private void SelectCare_Click(object sender, RoutedEventArgs e) => ApplyPlan(MaintenancePlan.Care);
    private void SelectBatch_Click(object sender, RoutedEventArgs e) { ApplyPlan(MaintenancePlan.FullBatch); Status.Text = "Full batch selected. Review advanced tasks and reboot before running."; }
    private async void Care_Click(object sender, RoutedEventArgs e) { ApplyPlan(MaintenancePlan.Care); await StartPlanAsync(SelectedPlan()); }
    private async void Run_Click(object sender, RoutedEventArgs e) => await StartPlanAsync(SelectedPlan());
    private async Task StartPlanAsync(MaintenancePlan plan)
    {
        if (!admin || busy) return;
        if (plan.IsEmpty) { Status.Text = "Select at least one task."; return; }
        string notice = "Run these tasks?\n\n" + plan.Describe() + "\n\nFile deletion is permanent. Locked/protected files and links are skipped.";
        if (plan.Prefetch) notice += "\nPrefetch removal may temporarily slow application launches.";
        if (plan.WindowsLogs) notice += "\nWindows Logs cleanup removes troubleshooting history.";
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
        try
        {
            long bytes = 0; int count = 0;
            foreach (var area in plan.CleanupAreas())
            {
                if (stopRequested) break;
                var preview = await Task.Run(() => WindowsOperations.Cleaner(area, plan.MinimumAgeDays).Scan(DateTime.UtcNow));
                bytes += preview.Bytes; count += preview.Files.Count;
                Log($"Preview {area}: {preview.Files.Count:N0} eligible files; {preview.Bytes:N0} bytes; {preview.Skipped} skipped entries.");
                foreach (var path in preview.Files.Take(60)) Log(path);
                if (preview.Files.Count > 60) Log("First 60 paths shown. Running rescans the selected roots.");
            }
            Status.Text = $"Preview only: {count:N0} files, {bytes / 1048576.0:N2} MB. Nothing deleted.";
        }
        catch (Exception ex) { Status.Text = "Preview failed."; Log(ex.Message); }
        finally { stopRequested = false; SetBusy(false); }
    }
    private void Stop_Click(object sender, RoutedEventArgs e) { stopRequested = true; StopButton.IsEnabled = false; Log("Stopping after current task; DHCP renewal is still attempted after release."); }
    private void Elevate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the executable.");
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Start RMPCS.exe directly to request admin rights.");
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory }); Close();
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
    private void StartupOn_Click(object sender, RoutedEventArgs e) { if (Confirm("Open RMPCS with administrator rights at sign-in for this Windows user? No maintenance runs just because the app opens.")) ChangeAutomation(() => TaskSchedulerService.Save(true)); }
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
        if (busy) { e.Cancel = true; MessageBox.Show(this, "Wait for the active task to finish. Stop after current task prevents later tasks from starting.", "Task running"); }
    }
}
