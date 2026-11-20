namespace RMPCS.Core;

public enum CleanupArea { UserTemp, WindowsTemp, Prefetch, WindowsLogs, Thumbnails, ErrorArchives, CrashDumps, RecycleBin }
public enum SystemCommand { FlushDns, RepairImage, ScanSystemFiles, ReleaseDhcp, RenewDhcp, Restart10, Restart60, Restart300, AbortRestart }

public sealed record MaintenancePlan
{
    public bool UserTemp { get; init; }
    public bool WindowsTemp { get; init; }
    public bool Prefetch { get; init; }
    public bool WindowsLogs { get; init; }
    public bool Thumbnails { get; init; }
    public bool ErrorArchives { get; init; }
    public bool CrashDumps { get; init; }
    public bool RecycleBin { get; init; }
    public bool ResetSearch { get; init; }
    public bool FlushDns { get; init; }
    public bool RepairImage { get; init; }
    public bool ScanFiles { get; init; }
    public bool RenewDhcp { get; init; }
    public bool Restart { get; init; }
    public int RestartSeconds { get; init; } = 60;
    public int MinimumAgeDays { get; init; } = 7;
    public bool IsEmpty => !CleanupAreas().Any() && !ResetSearch && !FlushDns && !RepairImage && !ScanFiles && !RenewDhcp && !Restart;
    public static MaintenancePlan Light => new() { UserTemp = true, WindowsTemp = true, FlushDns = true };
    public static MaintenancePlan Care => Light with { RepairImage = true, ScanFiles = true };
    public static MaintenancePlan FullBatch => Care with { Prefetch = true, WindowsLogs = true, ResetSearch = true, RenewDhcp = true, Restart = true, MinimumAgeDays = 0 };
    public IEnumerable<CleanupArea> CleanupAreas()
    {
        if (UserTemp) yield return CleanupArea.UserTemp;
        if (WindowsTemp) yield return CleanupArea.WindowsTemp;
        if (Prefetch) yield return CleanupArea.Prefetch;
        if (WindowsLogs) yield return CleanupArea.WindowsLogs;
        if (Thumbnails) yield return CleanupArea.Thumbnails;
        if (ErrorArchives) yield return CleanupArea.ErrorArchives;
        if (CrashDumps) yield return CleanupArea.CrashDumps;
        if (RecycleBin) yield return CleanupArea.RecycleBin;
    }
    public void Validate()
    {
        if (MinimumAgeDays is not (0 or 1 or 7 or 30)) throw new ArgumentException("Unsupported cleanup age.");
        if (RestartSeconds is not (10 or 60 or 300)) throw new ArgumentException("Unsupported restart countdown.");
    }
    public string Describe() => string.Join(Environment.NewLine,
        CleanupAreas().Select(x => x == CleanupArea.RecycleBin ? "• Permanently empty this user's Recycle Bins (all ages)" : $"• {x}: files older than {MinimumAgeDays} day(s)")
        .Concat(new[] { ResetSearch ? "• Reset and rebuild Windows Search index" : null, FlushDns ? "• Flush DNS cache" : null,
            RepairImage ? "• DISM image repair" : null, ScanFiles ? "• SFC system-file check" : null,
            RenewDhcp ? "• Disconnect DHCP adapters, then renew leases" : null, Restart ? $"• Restart after successful completion ({RestartSeconds}-second countdown)" : null }.OfType<string>()));
}

public interface IMaintenanceOperations
{
    Task<CleanupResult> CleanAsync(CleanupArea area, int minimumAgeDays);
    Task<int> RunAsync(SystemCommand command);
    Task ResetSearchAsync();
}

public sealed record MaintenanceResult(int Failed, int SkippedEntries, bool Stopped, bool RestartScheduled);

public static class MaintenanceRunner
{
    public static async Task<MaintenanceResult> RunAsync(MaintenancePlan plan, IMaintenanceOperations operations, Action<string> log, Func<bool>? shouldStop = null)
    {
        plan.Validate();
        int failed = 0, skipped = 0;
        bool restart = false;
        bool Stop() => shouldStop?.Invoke() == true;
        async Task<int> Command(SystemCommand command)
        {
            try { return await operations.RunAsync(command); }
            catch (Exception ex) { log($"{command} failed: {ex.Message}"); return -1; }
        }
        foreach (var area in plan.CleanupAreas())
        {
            if (Stop()) break;
            try
            {
                var result = await operations.CleanAsync(area, plan.MinimumAgeDays);
                skipped += result.Skipped;
                log($"{area}: deleted {result.Deleted:N0} entries, reclaimed {result.Bytes:N0} bytes, skipped {result.Skipped:N0} entries.");
            }
            catch (Exception ex) { failed++; log($"{area} failed: {ex.Message}"); }
        }
        if (plan.FlushDns && !Stop()) if (await Command(SystemCommand.FlushDns) != 0) failed++;
        bool imageReady = true;
        if (plan.RepairImage && !Stop())
        {
            int exit = await Command(SystemCommand.RepairImage);
            imageReady = exit is 0 or 3010;
            if (!imageReady) failed++;
        }
        if (plan.ScanFiles && !Stop())
        {
            if (!imageReady) { failed++; log("SFC skipped because DISM failed."); }
            else if (await Command(SystemCommand.ScanSystemFiles) != 0) failed++;
        }
        if (plan.ResetSearch && !Stop())
        {
            try { await operations.ResetSearchAsync(); log("Search reset accepted. Windows rebuilds the index in the background; indexing is not yet complete."); }
            catch (Exception ex) { failed++; log("Search reset failed: " + ex.Message); }
        }
        if (plan.RenewDhcp && !Stop())
        {
            if (await Command(SystemCommand.ReleaseDhcp) != 0) failed++;
            // A release failure or Stop click must not bypass the reconnection attempt.
            if (await Command(SystemCommand.RenewDhcp) != 0) failed++;
        }
        if (plan.Restart && !Stop())
        {
            if (failed != 0) log("Automatic restart suppressed because a task failed.");
            else
            {
                int exit = await Command(plan.RestartSeconds switch { 10 => SystemCommand.Restart10, 300 => SystemCommand.Restart300, _ => SystemCommand.Restart60 });
                restart = exit == 0;
                if (!restart) failed++;
            }
        }
        return new(failed, skipped, Stop(), restart);
    }
}
