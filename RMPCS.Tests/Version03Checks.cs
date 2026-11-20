using System.Text.Json;
using RMPCS.Core;

internal static class Version03Checks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        check(HealthRules.StorageAdvice(1L << 30, 100L << 30).StartsWith("Low free"), "health rule flags critically low drive space");
        check(HealthRules.StorageAdvice(20L << 30, 100L << 30).StartsWith("No low"), "health rule does not flag adequate drive space");
        check(HealthRules.StorageAdvice(0, 0).Contains("unavailable"), "unknown drive capacity is not reported as healthy");
        check(HealthRules.MemoryAdvice(90).StartsWith("High memory") && !HealthRules.MemoryAdvice(50).StartsWith("High memory"), "memory pressure thresholds distinguish snapshots");
        check(HealthRules.CpuAdvice(95).Contains("deferring") && !HealthRules.CpuAdvice(5).Contains("deferring"), "CPU pressure advice is conditional");
        string sid = "S-1-5-21-100-200-300-1001";
        var entry = new StartupItem(StartupPolicy.Id("User64", "Example", sid), "Example", "\"%LOCALAPPDATA%\\Example\\app.exe\" --startup", "User64", false, true, sid);
        var restored = JsonSerializer.Deserialize<StartupItem>(JsonSerializer.Serialize(entry));
        check(restored == entry && restored!.Expand && restored.Command.Contains("%LOCALAPPDATA%"), "startup backup preserves exact command and expansion metadata");
        StartupPolicy.Validate(entry, sid);
        bool rejected = false; try { StartupPolicy.Validate(entry, "S-1-5-21-999"); } catch (ArgumentException) { rejected = true; }
        check(rejected, "startup restore rejects a different user's backup");
        rejected = false; try { StartupPolicy.Validate(entry with { Scope = "RunOnce" }, sid); } catch (ArgumentException) { rejected = true; }
        check(rejected, "startup policy rejects RunOnce and arbitrary registry scopes");
        rejected = false; try { StartupPolicy.Validate(entry with { Id = "FORGED" }, sid); } catch (ArgumentException) { rejected = true; }
        check(rejected, "startup backup identity tampering is rejected");
        var machine = new StartupItem(StartupPolicy.Id("Machine32", "Example", "machine"), "Example", "app.exe", "Machine32", false, false, "machine");
        StartupPolicy.Validate(machine, sid);
        check(machine.Id != StartupPolicy.Id("Machine64", "Example", "machine"), "32-bit and 64-bit startup backup identities do not collide");
        rejected = false; try { StartupPolicy.Validate(machine with { OwnerSid = "arbitrary", Id = StartupPolicy.Id("Machine32", "Example", "arbitrary") }, sid); } catch (ArgumentException) { rejected = true; }
        check(rejected, "machine startup backups require the machine scope identity");
        var added = new MaintenancePlan { Thumbnails = true, ErrorArchives = true, CrashDumps = true, RecycleBin = true };
        check(!added.IsEmpty && added.CleanupAreas().SequenceEqual(new[] { CleanupArea.Thumbnails, CleanupArea.ErrorArchives, CleanupArea.CrashDumps, CleanupArea.RecycleBin }), "expanded cleanup categories are selectable independently");
        check(added.Describe().Contains("all ages"), "Recycle Bin confirmation explicitly ignores the age filter");
        check(!MaintenancePlan.Care.Thumbnails && !MaintenancePlan.Care.ErrorArchives && !MaintenancePlan.Care.CrashDumps && !MaintenancePlan.Care.RecycleBin && !MaintenancePlan.FullBatch.RecycleBin, "new cleanup categories stay outside presets and scheduled care");
        var fake = new RecordingOperations();
        await MaintenanceRunner.RunAsync(added, fake, _ => { });
        check(fake.Calls.Count == 4, "expanded categories route through the maintenance queue");
    }
    private sealed class RecordingOperations : IMaintenanceOperations
    {
        public List<CleanupArea> Calls { get; } = [];
        public Task<CleanupResult> CleanAsync(CleanupArea area, int days) { Calls.Add(area); return Task.FromResult(new CleanupResult(0, 0, 0)); }
        public Task<int> RunAsync(SystemCommand command) => throw new Exception("Unexpected system command.");
        public Task ResetSearchAsync() => throw new Exception("Unexpected search reset.");
    }
}
