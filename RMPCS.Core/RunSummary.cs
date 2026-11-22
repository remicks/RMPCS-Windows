using System.Text;
namespace RMPCS.Core;

public sealed record DriveSpace(string Drive, long? FreeBytes);
public static class RunSummary
{
    public static string Format(MaintenanceResult result, TimeSpan elapsed, IReadOnlyList<DriveSpace> before, IReadOnlyList<DriveSpace> after)
    {
        var text = new StringBuilder("RUN SUMMARY\n");
        text.AppendLine($"Duration: {elapsed.TotalSeconds:N1} seconds. Failed tasks: {result.Failed}. Skipped file entries: {result.SkippedEntries}.");
        text.AppendLine(result.RestartScheduled ? "Restart countdown active. Save work or cancel the pending restart." : result.Stopped ? "Queue stopped after the active task." : "Queue finished. Read repair output for findings; completion does not prove a repair succeeded.");
        long files = 0, bytes = 0, binItems = 0, binBytes = 0;
        bool invalid = false;
        foreach (var item in result.Cleanup.Where(x => x.Complete))
        {
            try
            {
                if (item.Deleted < 0 || item.Bytes < 0) throw new OverflowException();
                if (item.Area == CleanupArea.RecycleBin) { binItems = checked(binItems + item.Deleted); binBytes = checked(binBytes + item.Bytes); }
                else { files = checked(files + item.Deleted); bytes = checked(bytes + item.Bytes); }
            }
            catch (OverflowException) { invalid = true; }
        }
        if (invalid) text.AppendLine("Cleanup totals unavailable: returned counters exceeded the supported range.");
        else
        {
            text.AppendLine($"Confirmed deleted file entries: {files:N0}; logical size: {bytes:N0} bytes ({bytes / 1048576d:N2} MiB). This is not a physical free-space measurement.");
            if (result.Cleanup.Any(x => x.Area == CleanupArea.RecycleBin)) text.AppendLine($"Recycle Bin decrease (estimate): {binItems:N0} entries; {binBytes:N0} bytes. Concurrent bin activity can affect this estimate.");
        }
        if (result.Cleanup.Any(x => !x.Complete)) text.AppendLine("Some cleanup operations did not return complete counters; partial work may not be included.");
        text.AppendLine("OBSERVED DRIVE FREE SPACE (the app observes the Windows OS drive; other programs and Windows can change these values)");
        foreach (var a in before)
        {
            var b = after.FirstOrDefault(x => x.Drive.Equals(a.Drive, StringComparison.OrdinalIgnoreCase));
            if (a.FreeBytes is not >= 0 || b?.FreeBytes is not >= 0) { text.AppendLine(a.Drive + ": before/after observation unavailable."); continue; }
            long delta = b.FreeBytes.Value - a.FreeBytes.Value;
            text.AppendLine($"{a.Drive}: before {a.FreeBytes.Value:N0} bytes; after {b.FreeBytes.Value:N0} bytes; observed change {delta:+#,0;-#,0;0} bytes.");
        }
        if (before.Count == 0) text.AppendLine("No drive-space observations available.");
        text.AppendLine("Observed changes are not attributed solely to RMPCS. Compression, allocation, open file handles and concurrent work can differ from logical deletion sizes.");
        return text.ToString();
    }
}
