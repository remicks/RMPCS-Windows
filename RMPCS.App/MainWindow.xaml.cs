using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using Microsoft.Win32;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow : Window
{
    private bool busy, stopRequested, careApproved;
    private readonly bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    private readonly string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
    private string[] TempRoots() => new[]
    {
        UserTemp.IsChecked == true ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp") : null,
        SystemTemp.IsChecked == true ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") : null
    }.OfType<string>().ToArray();

    public MainWindow()
    {
        InitializeComponent();
        AdminStatus.Text = admin ? "Administrator session" : "Standard session";
        ElevateButton.IsEnabled = !admin;
        RunButton.IsEnabled = CareButton.IsEnabled = RestartButton.IsEnabled = admin;
        Log("RMPCS 0.1 — based on Wade's WMPCS 0.3b.");
        Log("Select tasks on the left. No maintenance has run.");
    }

    public void CheckAdministrator()
    {
        if (admin) return;
        Status.Text = "Administrator access required.";
        MessageBox.Show(this, "RMPCS needs administrator rights to perform maintenance. Right-click RMPCS.exe and select Run as administrator, or use the Run as administrator button in this window. Maintenance is disabled until you relaunch with admin rights.", "Run RMPCS as administrator", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Log(string text)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => Log(text)); return; }
        // Bound in-memory output so long-running commands cannot exhaust the UI.
        if (Activity.Text.Length > 180_000) Activity.Text = "[Earlier activity trimmed]\n" + Activity.Text[^120_000..];
        Activity.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        Activity.ScrollToEnd();
    }

    private void SetBusy(bool value)
    {
        busy = value;
        TaskPanel.IsEnabled = PreviewButton.IsEnabled = !value;
        RunButton.IsEnabled = RestartButton.IsEnabled = CareButton.IsEnabled = !value && admin;
        ElevateButton.IsEnabled = !value && !admin;
        StopButton.IsEnabled = value;
        Progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        Progress.IsIndeterminate = value;
    }

    private bool Confirm(string text) => MessageBox.Show(this, text, "RMPCS", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void Care_Click(object sender, RoutedEventArgs e)
    {
        if (!admin || busy) return;
        if (!Confirm("Run recommended care? This permanently removes eligible temp files older than 7 days, flushes DNS, and runs DISM followed by SFC. Repairs may use Windows Update and can take a long time. No network lease reset or restart is included.")) return;
        UserTemp.IsChecked = SystemTemp.IsChecked = Dns.IsChecked = Dism.IsChecked = Sfc.IsChecked = true;
        Dhcp.IsChecked = false;
        careApproved = true;
        Run_Click(sender, e);
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var roots = TempRoots();
        if (roots.Length == 0) { Status.Text = "Select a temporary-file task first."; return; }
        SetBusy(true);
        try
        {
            var preview = await Task.Run(() => new TempCleaner(roots).Scan(DateTime.UtcNow));
            Status.Text = $"Preview: {preview.Files.Count:N0} files · {FormatBytes(preview.Bytes)}";
            foreach (var root in roots) Log("Preview root: " + root);
            Log($"Eligible: {preview.Files.Count:N0} files, {FormatBytes(preview.Bytes)}. Inaccessible/linked entries skipped: {preview.Skipped}.");
            foreach (var file in preview.Files.Take(100)) Log(file);
            if (preview.Files.Count > 100) Log("Showing the first 100 paths. Run selected tasks rescans before asking you to confirm deletion.");
        }
        catch (Exception ex) { Status.Text = "Preview failed."; Log(ex.Message); }
        finally { SetBusy(false); }
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (!admin || busy) return;
        var roots = TempRoots();
        bool dns = Dns.IsChecked == true, dism = Dism.IsChecked == true, sfc = Sfc.IsChecked == true, dhcp = Dhcp.IsChecked == true;
        if (roots.Length == 0 && !dns && !dism && !sfc && !dhcp) { Status.Text = "Select at least one task."; return; }
        if (!admin && (SystemTemp.IsChecked == true || dns || dism || sfc || dhcp))
        { MessageBox.Show(this, "Use Run as administrator, then select your system tasks again.", "Administrator needed"); return; }
        if (dhcp && !Confirm("Release and renew the DHCP lease on all DHCP adapters? This interrupts network access, including remote sessions and possibly VPNs. Reconnection is not guaranteed.")) return;
        SetBusy(true);
        stopRequested = false;
        int issues = 0;
        try
        {
            if (roots.Length > 0)
            {
                Status.Text = "Scanning temporary files…";
                var cleaner = new TempCleaner(roots);
                var preview = await Task.Run(() => cleaner.Scan(DateTime.UtcNow));
                Log($"Temp scan: {preview.Files.Count:N0} eligible files, {FormatBytes(preview.Bytes)}; {preview.Skipped} inaccessible/linked entries skipped.");
                if (!stopRequested && preview.Files.Count > 0)
                {
                    string targetList = string.Join(Environment.NewLine, roots);
                    if (!careApproved && !Confirm($"Permanently delete up to {preview.Files.Count:N0} temporary files ({FormatBytes(preview.Bytes)}) from:\n\n{targetList}\n\nOnly files created and modified over 7 days ago qualify. Files do not go to the Recycle Bin."))
                    { Log("Cleanup declined; remaining queue stopped."); stopRequested = true; }
                    else
                    {
                        Status.Text = "Cleaning temporary files…";
                        var result = await Task.Run(() => cleaner.Clean(preview));
                        Log($"Deleted {result.Deleted:N0} files; reclaimed {FormatBytes(result.Bytes)}; skipped {result.Skipped:N0} entries.");
                        if (result.Skipped > 0) issues++;
                    }
                }
                else if (preview.Skipped > 0) issues++;
            }
            if (dns && !stopRequested) if (await RunTool("Flush DNS cache", "ipconfig.exe", "/flushdns") != 0) issues++;
            bool imageReady = true;
            if (dism && !stopRequested)
            {
                int exit = await RunTool("Repair Windows image", "DISM.exe", "/Online", "/Cleanup-Image", "/RestoreHealth");
                imageReady = exit is 0 or 3010;
                if (!imageReady) issues++;
            }
            if (sfc && !stopRequested)
            {
                if (imageReady) { if (await RunTool("Check system files", "sfc.exe", "/scannow") != 0) issues++; }
                else { Log("SFC skipped because DISM failed. Resolve the image repair error before retrying."); issues++; }
            }
            // Network interruption is last so it cannot disrupt DISM's downloads.
            if (dhcp && !stopRequested)
            {
                if (await RunTool("Release DHCP lease", "ipconfig.exe", "/release") != 0) issues++;
                // Always attempt renewal once release was requested, even if Stop was clicked.
                if (await RunTool("Renew DHCP lease", "ipconfig.exe", "/renew") != 0) issues++;
            }
            Status.Text = stopRequested ? "Queue stopped. See activity for completed tasks." : issues > 0 ? "Finished with issues or skipped items. Review activity." : "Tasks finished. Review command output for repair findings.";
            Log(Status.Text);
        }
        catch (Exception ex) { Status.Text = "Task failed. Review activity."; Log(ex.Message); }
        finally { careApproved = false; SetBusy(false); }
    }

    private async Task<int> RunTool(string label, string executable, params string[] arguments)
    {
        Status.Text = label + "…";
        Log(label + ": " + executable + " " + string.Join(" ", arguments));
        var start = new ProcessStartInfo(Path.Combine(systemDirectory, executable))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = systemDirectory };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        process.Start();
        // Some Windows tools emit UTF-16 while others use the OEM encoding. Detect BOMs.
        var output = Pump(process.StandardOutput);
        var error = Pump(process.StandardError);
        await Task.WhenAll(output, error, process.WaitForExitAsync());
        Log($"{label}: exit code {process.ExitCode}." + (process.ExitCode == 3010 ? " Windows requests a restart." : ""));
        return process.ExitCode;
    }

    private async Task Pump(StreamReader reader)
    {
        var buffer = new char[512];
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
        {
            var chunk = new string(buffer, 0, count).Replace("\0", "").Replace('\r', '\n');
            foreach (var line in chunk.Split('\n', StringSplitOptions.RemoveEmptyEntries)) Log(line);
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) { stopRequested = true; StopButton.IsEnabled = false; Log("Stop requested. The current operation will finish; DHCP renewal will still be attempted after release."); }
    private void Elevate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the app executable.");
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Start RMPCS.exe directly before requesting administrator rights.");
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory });
            Close();
        }
        catch (Exception ex) { Log("Administrator relaunch was not completed: " + ex.Message); }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = $"RMPCS-{DateTime.Now:yyyyMMdd-HHmmss}.txt", Filter = "Text log (*.txt)|*.txt" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, Activity.Text); Log("Activity exported."); }
        catch (Exception ex) { Log("Export failed: " + ex.Message); }
    }
    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm("Schedule a PC restart in 60 seconds? Save your work first. Windows may force apps to close when the countdown ends. Use Cancel restart to abort.")) return;
        SetBusy(true);
        try { int exit = await RunTool("Schedule restart", "shutdown.exe", "/r", "/t", "60"); Status.Text = exit == 0 ? "Restart scheduled. Save your work now." : "Could not schedule restart. Review activity."; }
        catch (Exception ex) { Log(ex.Message); }
        finally { SetBusy(false); }
    }
    private async void AbortRestart_Click(object sender, RoutedEventArgs e)
    {
        AbortRestartButton.IsEnabled = false;
        try { int exit = await RunTool("Cancel pending restart", "shutdown.exe", "/a"); if (!busy) Status.Text = exit == 0 ? "Pending restart cancelled." : "No restart cancelled. Review activity."; }
        catch (Exception ex) { Log(ex.Message); }
        finally { AbortRestartButton.IsEnabled = true; }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; MessageBox.Show(this, "Wait for the current operation to finish. Stop after current task prevents later tasks from starting.", "Task running"); }
    }
    private static string FormatBytes(long bytes) => bytes >= 1_073_741_824 ? $"{bytes / 1_073_741_824.0:N2} GB" : $"{bytes / 1_048_576.0:N2} MB";
}
