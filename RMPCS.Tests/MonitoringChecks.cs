using RMPCS.Core;

internal static class MonitoringChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(!new AppPreferences().Overlay.Enabled && !AppPreferences.Parse("{}").Overlay.Enabled && !AppPreferences.Parse("{\"Overlay\":null}").Overlay.Enabled, "overlay stays opt-in for new, old and null preferences");
        var options = new OverlayPreferences { Enabled = true, IntervalSeconds = 0, TextSize = 90, Width = 999, OpacityPercent = -100, Font = @"C:\untrusted.ttf", Monitor = "web://display", Position = "bad", X = double.NaN, Y = double.PositiveInfinity, Cpu = false, Memory = false, Network = false, DiskSpace = false, Uptime = false }.Normalize();
        check(options.Enabled && options.IntervalSeconds == 2 && options.TextSize == 14 && options.Width == 280 && options.OpacityPercent == 50 && options.Font == "Barlow" && options.Monitor == "Current" && options.Position == "Top right" && options.X == .95 && options.Y == .05 && !options.Cpu, "malformed overlay settings normalize to bounded allowlisted choices without silently enabling metrics");
        check((options with { OpacityPercent = 999 }).Normalize().OpacityPercent == 100 && (options with { OpacityPercent = 75 }).Normalize().OpacityPercent == 80, "opacity is readable and matches the UI choices");
        var beforePreferences = new AppPreferences(); var afterPreferences = beforePreferences with { Overlay = options };
        check(AppPreferences.Parse(afterPreferences.ToJson()) == afterPreferences.Normalize(), "overlay preferences round-trip with value equality");
        check(PreferenceUndo.Parse(new PreferenceUndo(beforePreferences, afterPreferences).ToJson()).Restore(AppPreferences.Parse(afterPreferences.ToJson())) == beforePreferences.Normalize(), "preference undo preserves nested overlay choices");
        bool conflict = false; try { new PreferenceUndo(beforePreferences, afterPreferences).Restore(afterPreferences with { Overlay = options with { Locked = true } }); } catch (InvalidOperationException) { conflict = true; }
        check(conflict, "undo does not overwrite later conflicting overlay choices");
        foreach (int seconds in new[] { 1, 2, 5 })
        {
            double budget = Math.Max(5, seconds * 3);
            check(Monitoring.Fresh(budget, seconds) && !Monitoring.Fresh(budget + .1, seconds), "slow counter reads are discarded after the selected freshness budget");
        }
        check(!Monitoring.Fresh(double.NaN, 2) && !Monitoring.Fresh(double.PositiveInfinity, 2) && !Monitoring.Fresh(-1, 2), "invalid elapsed times cannot mark a sample fresh");
        Dictionary<string, NetworkBytes> Counters(long down, long up, string id = "fixture") => new() { [id] = new(down, up) };
        var a = new MonitorSample(10, new(20, 100, 100), 1000, 400, Counters(1000, 500), 123, 1000);
        var b = a with { Time = 12, Cpu = new(30, 140, 160), Network = Counters(3000, 1500) };
        var value = Monitoring.Read(a, b);
        check(value.Cpu == 90 && value.MemoryPercent == 60 && value.MemoryUsed == 600 && value.DownloadBytes == 1000 && value.UploadBytes == 500 && value.DiskFree == 123, "CPU, memory and network rates use independent correct deltas and units");
        check(Monitoring.Read(null, b).Cpu is null && Monitoring.Read(null, b).DownloadBytes is null && Monitoring.Read(null, b).MemoryUsed == 600, "initial CPU/network sample warms up while current RAM remains available");
        foreach (double time in new[] { 10, 9, 10.1, 41, double.NaN, double.PositiveInfinity })
            check(Monitoring.Read(a, b with { Time = time }).Cpu is null && Monitoring.Read(a, b with { Time = time }).DownloadBytes is null, "invalid, suspended or too-short sampling intervals do not create spikes");
        foreach (var counters in new IReadOnlyDictionary<string, NetworkBytes>?[] { null, new Dictionary<string, NetworkBytes>(), Counters(3000, 1500, "different"), Counters(500, 1500), Counters(3000, 50) })
            check(Monitoring.Read(a, b with { Network = counters }).DownloadBytes is null, "missing, changed and reset network counters show unavailable");
        check(Monitoring.Read(a, b with { Cpu = new(1, 140, 160) }).Cpu is null && Monitoring.Read(a, b with { Cpu = new(3000, 140, 160) }).Cpu is null, "CPU reset or impossible idle counters are rejected");
        check(Monitoring.Read(a, b with { AvailableMemory = 1001 }).MemoryUsed is null && Monitoring.Read(a, b with { TotalMemory = 0 }).MemoryPercent is null, "invalid memory counters do not display false usage");
        check(Monitoring.Read(a, b with { UptimeMilliseconds = -10 }).UptimeMilliseconds == 0 && Monitoring.Read(a, b with { UptimeMilliseconds = long.MaxValue }).UptimeMilliseconds <= TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond, "uptime remains a valid bounded duration");
        foreach (var screen in new[] { new ScreenArea(-1920, -100, 0, 980), new(0, 0, 3840, 2160), new(100, 100, 180, 160) })
            foreach (string position in OverlayPreferences.Positions)
                foreach (int size in new[] { 1, 280, 10000 })
                {
                    var placed = Monitoring.Place(screen, size, size, new() { Position = position, X = 2, Y = -1 });
                    check(placed.Left >= screen.Left && placed.Top >= screen.Top && placed.Right <= screen.Right && placed.Bottom <= screen.Bottom && placed.Width >= 1 && placed.Height >= 1, "overlay remains inside negative, high-resolution and tiny monitor work areas");
                }
        bool badArea = false; try { Monitoring.Place(new(0, 0, 0, 0), 100, 100, new()); } catch (ArgumentException) { badArea = true; }
        check(badArea, "missing monitor work area is rejected");
        var guide = new SetupGuide(); for (int i = 0; i < 100; i++) guide.Back();
        check(guide.Index == 0 && !guide.CanBack && guide.CanNext, "guide Back cannot leave the first step");
        for (int i = 0; i < 100; i++) guide.Next();
        check(guide.Index == SetupGuide.Steps.Count - 1 && !guide.CanNext && guide.CanBack, "guide Next cannot leave the final step");
        for (int i = 0; i < 100; i++) guide.Back();
        check(guide.Index == 0 && SetupGuide.Steps.Select(s => s.Key).Distinct().Count() == SetupGuide.Steps.Count && SetupGuide.Steps.All(s => AppPreferences.Pages.Contains(s.Page)), "guide steps have unique identities and valid existing app sections");
        check(new AppPreferences() == beforePreferences && !beforePreferences.Overlay.Enabled, "guide navigation has no preference or monitoring side effects");
    }
}
