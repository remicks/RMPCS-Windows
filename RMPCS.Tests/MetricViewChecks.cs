using RMPCS.Core;

internal static class MetricViewChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var legacy = AppPreferences.Parse("{\"Overlay\":{\"Graphs\":false}}").Overlay;
        check(legacy.CpuView is { Numbers: true, Graph: false } && legacy.MemoryView is { Numbers: true, Graph: false }, "v0.13.5 graph-off preferences migrate without changing numeric views");
        var previous = new OverlayPreferences().Normalize();
        check(previous.CpuView!.Graph && previous.MemoryView!.Graph && !previous.NetworkView!.Graph, "legacy default CPU/RAM graphs migrate to metric-specific choices");
        for (int mask = 0; mask < 32; mask++)
        {
            var views = new MetricViewPreferences { Numbers = (mask & 1) != 0, Circle = (mask & 2) != 0, CircleWithValue = (mask & 4) != 0, Bar = (mask & 8) != 0, Graph = (mask & 16) != 0 };
            var chosen = new OverlayPreferences { Graphs = false, CpuView = views, MemoryView = views, NetworkView = views, DiskView = views, UptimeView = views }.Normalize();
            var read = AppPreferences.Parse(new AppPreferences { Overlay = chosen }.ToJson()).Overlay;
            check(read == chosen && read.Cpu && read.Memory && read.Network && read.DiskSpace && read.Uptime, "all five display toggles round-trip independently without changing metric selection");
            var sampling = read.SamplingChoices();
            check(sampling.Cpu == (mask != 0) && sampling.Network == (mask != 0) && read.HasVisibleMetrics() == (mask != 0), "hidden metrics do not incur sampling but retain their on/off selections");
            check(!(read with { Cpu = false }).SamplingChoices().Cpu && (read with { Cpu = false }).CpuView == views, "turning a metric off preserves its chosen display combination");
        }
        var allOff = new OverlayPreferences { Cpu = false, Memory = false, Network = false, DiskSpace = false, Uptime = false }.Normalize();
        check(!allOff.Cpu && !allOff.HasVisibleMetrics(), "all metric switches off stays off without silently restoring CPU");
        var explicitGraph = new OverlayPreferences { Graphs = false, CpuView = new() { Graph = true } }.Normalize();
        check(explicitGraph.CpuView!.Graph, "legacy global graph flag does not override explicit per-metric views");
        check(new OverlayPreferences { NetworkScaleMiB = -1 }.Normalize().NetworkScaleMiB == 10, "network reference is allowlisted and always positive");
        check(Monitoring.RelativePercent(20971520, 10485760) == 200 && Monitoring.RelativePercent(0, 10) == 0, "reference gauges preserve true zero and above-scale values for labeling");
        foreach (double? value in new double?[] { null, -1, double.NaN, double.PositiveInfinity }) check(Monitoring.RelativePercent(value, 100) is null, "invalid reference gauge input stays unavailable");
        check(Monitoring.RelativePercent(10, 0) is null && Monitoring.RelativePercent(10, double.NaN) is null, "invalid reference cannot produce a gauge percentage");
        var sample = new MonitorSample(1, null, null, null, null, 25, 1000, 100);
        check(Monitoring.Read(null, sample).DiskAvailablePercent == 25 && Monitoring.Read(null, sample with { DiskCapacity = null }).DiskAvailablePercent is null && Monitoring.Read(null, sample with { DiskFree = 101 }).DiskAvailablePercent is null, "drive-space percentage requires valid capacity and never substitutes a fake value");
    }
}
