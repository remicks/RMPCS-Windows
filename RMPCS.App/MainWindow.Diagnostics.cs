using System.IO;
using System.Windows;
using Microsoft.Win32;
using RMPCS.Core;
namespace RMPCS.App;
public partial class MainWindow
{
    private NetworkSample? networkCurrent, networkBaseline;
    private string diagnosticReport = "";
    private void ShowDiagnosticReport(string report) { diagnosticReport = report; Activity.Text = report; Status.Text = "Diagnostics complete. No settings were changed."; ShowSidebarResults(); }
    private async void StabilityReport_Click(object sender, RoutedEventArgs e)
    {
        if (busy || TestMode) return;
        int days = StabilityPeriod.SelectedIndex switch { 0 => 1, 2 => 30, _ => 7 };
        SetBusy(true); StopButton.IsEnabled = false;
        try { ShowDiagnosticReport(await DiagnosticsService.StabilityAsync(days)); DiagnosticStatus.Text = "Stability metadata read. Counts describe a bounded event sample, not a diagnosis."; }
        catch { DiagnosticStatus.Text = "Stability report unavailable. No settings were changed."; }
        finally { SetBusy(false); }
    }
    private async void NetworkReport_Click(object sender, RoutedEventArgs e)
    {
        if (busy || TestMode) return;
        string target = NetworkTarget.SelectedIndex == 1 ? "github.com" : "www.microsoft.com";
        SetBusy(true); StopButton.IsEnabled = false; DiagnosticStatus.Text = "Running bounded DNS / HTTPS / gateway tests...";
        try
        {
            var result = await DiagnosticsService.NetworkAsync(target, windowLifetime.Token);
            networkCurrent = result;
            ShowDiagnosticReport(result.Format() + (networkBaseline is null ? "" : "\nCOMPARISON\n" + NetworkSample.Compare(networkBaseline, result)));
            DiagnosticStatus.Text = "Network sample complete. No reset or repair was performed.";
        }
        catch (OperationCanceledException) { DiagnosticStatus.Text = "Network diagnostics cancelled or timed out. No complete new sample was saved."; }
        catch { DiagnosticStatus.Text = "Network diagnostics unavailable. No settings were changed."; }
        finally { SetBusy(false); }
    }
    private void NetworkBaseline_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (networkCurrent is null) { DiagnosticStatus.Text = "Run network tests first."; return; }
        networkBaseline = networkCurrent; ShowDiagnosticReport(networkBaseline.Format());
        DiagnosticStatus.Text = "Baseline held in memory from " + networkBaseline.Time.ToString("g") + ". Run again to compare, or export to keep it.";
    }
    private void DiagnosticExport_Click(object sender, RoutedEventArgs e)
    {
        if (busy || diagnosticReport.Length == 0) return;
        var dialog = new SaveFileDialog { Title = "Export diagnostic report - choose a new local filename", FileName = $"RMPCS-diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.txt", Filter = "Text report (*.txt)|*.txt", OverwritePrompt = false };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            // A new file held without sharing prevents overwrite/hard-link redirection.
            string path = Path.GetFullPath(dialog.FileName);
            if (path.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(path)!).DriveType != DriveType.Fixed) throw new IOException("Choose a local fixed-drive destination.");
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            DiagnosticArchive.ValidateDestination(stream, path);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)); writer.Write(diagnosticReport);
            DiagnosticStatus.Text = "Report exported. Review it before sharing; nothing was uploaded.";
        }
        catch { DiagnosticStatus.Text = "Export failed. Choose a new filename on a local fixed drive without linked folders. Existing files are never overwritten."; }
    }
    private async void CreateRestore_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !admin || TestMode || !Confirm("Ask Windows to create a restore point now?\n\nSystem Restore is not a backup of deleted personal files. Creation may be unavailable or frequency-limited. RMPCS will verify the result without changing System Protection settings.")) return;
        SetBusy(true); StopButton.IsEnabled = false; ProtectionStatus.Text = "Requesting and verifying a new restore point...";
        try { using var lease = RunLease.Acquire(); ProtectionStatus.Text = lease is null ? "Another maintenance session is active. Try again later." : (await RestorePoints.CreateAsync()).Message; }
        catch { ProtectionStatus.Text = "Restore-point creation could not be verified."; }
        finally { SetBusy(false); }
    }
    private void UndoInterface_Click(object sender, RoutedEventArgs e)
    {
        if (busy || TestMode || !Confirm("Undo the most recent saved interface preference change?\n\nThis changes only RMPCS preferences for this Windows identity. Maintenance actions and schedules are unaffected.")) return;
        try { preferences = SettingsStore.UndoInterface(); PutPreferenceControls(); ApplyPreferences(); ProtectionStatus.Text = "Previous interface preferences restored. Launch preferences apply next time."; }
        catch (Exception ex) { ProtectionStatus.Text = "Interface undo unavailable: " + ex.Message; }
    }
    private void UndoAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !admin || TestMode || !TaskSchedulerService.IsInstalled || !Confirm("Undo the last installed administrator preference change made by this Windows identity?\n\nThis preference applies across Windows users. Windows UAC remains active.")) return;
        try { SettingsStore.UndoAdministrator(); RefreshInstallPreference(); ProtectionStatus.Text = "Previous administrator preference restored for the next launch."; }
        catch (Exception ex) { ProtectionStatus.Text = "Administrator preference undo unavailable: " + ex.Message; }
    }
}
