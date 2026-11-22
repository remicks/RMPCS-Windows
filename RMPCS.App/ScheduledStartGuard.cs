using System.Diagnostics;
using RMPCS.Core;
namespace RMPCS.App;
internal static class ScheduledStartGuard
{
    public static async Task<bool> MayStartAsync(ScheduleLaunch options, Action<string> log)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (!options.InWindow(DateTime.Now)) { log("Skipped: outside the authorized maintenance start window. No maintenance started."); return false; }
            if (options.DeferMinutes == 0) return true;
            WorkloadSample sample;
            try { sample = await WorkloadProbe.SampleAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { log("Skipped: workload preflight did not finish within 10 seconds. No maintenance started."); return false; }
            catch { sample = new(null,null,null,null,null); }
            string? reason = WorkloadRules.DeferReason(sample, options.RequireIdle, options.RequireAc);
            if (!options.InWindow(DateTime.Now)) { log("Skipped: the start window closed during preflight."); return false; }
            if (clock.Elapsed >= TimeSpan.FromMinutes(options.DeferMinutes)) { log("Skipped: workload deferral time expired during preflight."); return false; }
            if (reason is null) { log("Workload preflight allowed this start. Signals are best effort, not a guarantee that all games/calls/downloads are detected."); return true; }
            if (clock.Elapsed >= TimeSpan.FromMinutes(options.DeferMinutes)) { log("Skipped after bounded workload deferral: " + reason); return false; }
            log("Deferring start; recheck in at most 60 seconds: " + reason);
            var remaining = TimeSpan.FromMinutes(options.DeferMinutes) - clock.Elapsed;
            if (remaining <= TimeSpan.Zero) { log("Skipped: workload deferral time expired."); return false; }
            await Task.Delay(remaining < TimeSpan.FromSeconds(60) ? remaining : TimeSpan.FromSeconds(60));
        }
    }
}
