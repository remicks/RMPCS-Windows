using System.IO;
using System.Windows;
using Microsoft.Win32;
using RMPCS.Core;
namespace RMPCS.App;

public partial class MainWindow
{
    private long? lastCompletionNotice;
    internal void NotifyCompletion(CompletionNotice notice)
    {
        if (TestMode || !preferences.CompletionNotifications || tray?.Available != true || !Enum.IsDefined(notice)) return;
        long now = Environment.TickCount64;
        if (lastCompletionNotice is long previous && now - previous < 10000) return;
        lastCompletionNotice = now;
        tray.Notify(CompletionNotices.Text(notice));
    }
    private async void ArchiveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryFiles.SelectedItem is FileInfo file) await ArchiveLogsAsync([file.FullName]);
        else Status.Text = "Select a saved run in History first.";
    }
    private async void ArchiveRecent_Click(object sender, RoutedEventArgs e)
    {
        try { await ArchiveLogsAsync(SessionLog.List()); }
        catch (Exception ex) { Log("Archive selection unavailable: " + ex.Message); }
    }
    private async Task ArchiveLogsAsync(IReadOnlyList<string> selected)
    {
        if (busy || TestMode) return;
        if (selected.Count == 0) { Status.Text = "There are no saved run logs to archive."; return; }
        if (!Confirm($"Create a local ZIP archive of {selected.Count} selected saved RMPCS run log(s)?\n\nLogs can contain private paths and command output. Review the ZIP before sharing. Only app session logs are included; no Windows event logs, browser data or crash dumps are collected. Active/linked/oversized files are skipped. Originals are kept. Archives are not encrypted and are never uploaded by RMPCS.")) return;
        var dialog = new SaveFileDialog { Title = "Save diagnostic archive — choose a new local filename", FileName = $"RMPCS-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}.zip", Filter = "ZIP archive (*.zip)|*.zip", AddExtension = true, DefaultExt = ".zip", OverwritePrompt = false };
        if (dialog.ShowDialog(this) != true) return;
        SetBusy(true); Status.Text = "Creating diagnostic archive...";
        StopButton.IsEnabled = false; StopButton.Visibility = Visibility.Collapsed;
        bool created = false;
        try
        {
            var names = selected.Select(Path.GetFileName).OfType<string>().ToArray();
            var result = await Task.Run(() =>
            {
                string path = Path.GetFullPath(dialog.FileName);
                if (path.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(path)!).DriveType != DriveType.Fixed) throw new IOException("Choose a local fixed-drive archive destination.");
                // CreateNew refuses existing files/hard links. Verification precedes all private log writes.
                using var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                created = true;
                DiagnosticArchive.ValidateDestination(output, path);
                return DiagnosticArchive.Create(output, SessionLog.Folder, names);
            });
            Status.Text = $"Archive created: {result.Included} logs, {result.Skipped} skipped; {result.SourceBytes:N0} source bytes. Review ARCHIVE-README.txt before sharing.";
            Log(Status.Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { Status.Text = created ? "Archive could not be completed. The newly created ZIP is incomplete; original logs are unchanged." : "Archive could not be created. Choose a new filename on a local fixed drive. Existing files are never overwritten."; Log(ex.Message); }
        finally { SetBusy(false); }
    }
}
