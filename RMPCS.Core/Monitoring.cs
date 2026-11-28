namespace RMPCS.Core;

public sealed record MetricViewPreferences
{
    public bool Numbers { get; init; } = true;
    public bool Circle { get; init; }
    public bool CircleWithValue { get; init; }
    public bool Bar { get; init; }
    public bool Graph { get; init; }
    public bool Any() => Numbers || Circle || CircleWithValue || Bar || Graph;
}

public sealed record OverlayPreferences
{
    public bool Enabled { get; init; }
    public bool Cpu { get; init; } = true;
    public bool Memory { get; init; } = true;
    public bool Network { get; init; } = true;
    public bool DiskSpace { get; init; } = true;
    public bool Uptime { get; init; } = true;
    public bool Graphs { get; init; } = true;
    // Graphs is retained only to migrate v0.13.5 preferences when per-metric views are absent.
    public MetricViewPreferences? CpuView { get; init; }
    public MetricViewPreferences? MemoryView { get; init; }
    public MetricViewPreferences? NetworkView { get; init; }
    public MetricViewPreferences? DiskView { get; init; }
    public MetricViewPreferences? UptimeView { get; init; }
    public int NetworkScaleMiB { get; init; } = 10;
    public bool Topmost { get; init; } = true;
    public bool Locked { get; init; }
    public bool ClickThrough { get; init; }
    public bool Hotkey { get; init; } = true;
    public int IntervalSeconds { get; init; } = 2;
    public int TextSize { get; init; } = 14;
    public int Width { get; init; } = 280;
    public int OpacityPercent { get; init; } = 90;
    public string Font { get; init; } = "App font";
    public string Monitor { get; init; } = "Current";
    public string Position { get; init; } = "Top right";
    public double X { get; init; } = .95;
    public double Y { get; init; } = .05;
    public static readonly string[] Positions = ["Top left", "Top right", "Bottom left", "Bottom right", "Custom"];
    public OverlayPreferences Normalize() => this with
    {
        CpuView = CpuView ?? new() { Graph = Graphs }, MemoryView = MemoryView ?? new() { Graph = Graphs },
        NetworkView = NetworkView ?? new(), DiskView = DiskView ?? new(), UptimeView = UptimeView ?? new(),
        NetworkScaleMiB = NetworkScaleMiB is 1 or 10 or 100 or 1000 ? NetworkScaleMiB : 10,
        IntervalSeconds = IntervalSeconds is 1 or 2 or 5 ? IntervalSeconds : 2,
        TextSize = TextSize is 12 or 14 or 16 or 18 ? TextSize : 14,
        Width = Width is 220 or 280 or 340 ? Width : 280,
        OpacityPercent = (int)Math.Round(Math.Clamp(OpacityPercent, 50, 100) / 10d, MidpointRounding.AwayFromZero) * 10,
        Font = Font == "App font" ? Font : FontCatalog.Normalize(Font),
        Monitor = SidebarLayout.ValidMonitor(Monitor) ? Monitor : "Current",
        Position = Positions.Contains(Position, StringComparer.Ordinal) ? Position : "Top right",
        X = double.IsFinite(X) ? Math.Clamp(X, 0, 1) : .95,
        Y = double.IsFinite(Y) ? Math.Clamp(Y, 0, 1) : .05
    };
    public bool HasVisibleMetrics() => Cpu && (CpuView?.Any() ?? true) || Memory && (MemoryView?.Any() ?? true) ||
        Network && (NetworkView?.Any() ?? true) || DiskSpace && (DiskView?.Any() ?? true) || Uptime && (UptimeView?.Any() ?? true);
    public OverlayPreferences SamplingChoices() => this with { Cpu = Cpu && (CpuView?.Any() ?? true),
        Memory = Memory && (MemoryView?.Any() ?? true), Network = Network && (NetworkView?.Any() ?? true),
        DiskSpace = DiskSpace && (DiskView?.Any() ?? true), Uptime = Uptime && (UptimeView?.Any() ?? true) };
}

public readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
public readonly record struct NetworkBytes(long Received, long Sent);
public sealed record MonitorSample(double Time, CpuTimes? Cpu, ulong? TotalMemory, ulong? AvailableMemory,
    IReadOnlyDictionary<string, NetworkBytes>? Network, ulong? DiskFree, long UptimeMilliseconds, ulong? DiskCapacity = null);
public sealed record MonitorReading(double? Cpu, double? MemoryPercent, ulong? MemoryUsed, ulong? MemoryTotal,
    double? DownloadBytes, double? UploadBytes, ulong? DiskFree, long UptimeMilliseconds, double? DiskAvailablePercent = null);

public static class Monitoring
{
    public static bool Fresh(double elapsedSeconds, int intervalSeconds) => double.IsFinite(elapsedSeconds) && elapsedSeconds >= 0 &&
        elapsedSeconds <= Math.Max(5, (intervalSeconds is 1 or 2 or 5 ? intervalSeconds : 2) * 3);
    public static MonitorReading Read(MonitorSample? before, MonitorSample current)
    {
        double seconds = before is null ? 0 : current.Time - before.Time;
        bool interval = seconds is >= .5 and <= 30 && double.IsFinite(seconds);
        double? cpu = null, down = null, up = null;
        if (interval && before!.Cpu is CpuTimes a && current.Cpu is CpuTimes b && b.Idle >= a.Idle && b.Kernel >= a.Kernel && b.User >= a.User)
        {
            double total = (double)(b.Kernel - a.Kernel) + (b.User - a.User), idle = b.Idle - a.Idle;
            if (total > 0 && idle <= total) cpu = Math.Clamp(100 * (1 - idle / total), 0, 100);
        }
        if (interval && before!.Network is { Count: > 0 } old && current.Network is { Count: > 0 } next && old.Count == next.Count)
        {
            double received = 0, sent = 0; bool valid = true;
            foreach (var (id, value) in next)
            {
                if (!old.TryGetValue(id, out var prior) || value.Received < prior.Received || value.Sent < prior.Sent || prior.Received < 0 || prior.Sent < 0) { valid = false; break; }
                received += value.Received - prior.Received; sent += value.Sent - prior.Sent;
            }
            if (valid) { down = received / seconds; up = sent / seconds; }
        }
        ulong? used = current.TotalMemory is > 0 && current.AvailableMemory is ulong available && available <= current.TotalMemory.Value ? current.TotalMemory.Value - available : null;
        double? memory = used is ulong count ? 100d * count / current.TotalMemory!.Value : null;
        double? disk = current.DiskCapacity is > 0 && current.DiskFree is ulong free && free <= current.DiskCapacity.Value ? 100d * free / current.DiskCapacity.Value : null;
        return new(cpu, memory, used, used is null ? null : current.TotalMemory, down, up, current.DiskFree, Math.Clamp(current.UptimeMilliseconds, 0, TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond), disk);
    }
    public static double? RelativePercent(double? value, double reference) => value is double n && double.IsFinite(n) && n >= 0 && double.IsFinite(reference) && reference > 0 && double.IsFinite(n / reference * 100) ? n / reference * 100 : null;
    public static ScreenArea Place(ScreenArea work, int width, int height, OverlayPreferences options)
    {
        if (work.Width <= 0 || work.Height <= 0) throw new ArgumentException("No usable monitor work area.");
        options = options.Normalize(); width = Math.Clamp(width, 1, work.Width); height = Math.Clamp(height, 1, work.Height);
        int dx = work.Width - width, dy = work.Height - height;
        int x = options.Position == "Custom" ? (int)Math.Round(dx * options.X) : options.Position.EndsWith("left", StringComparison.Ordinal) ? Math.Min(12, dx) : Math.Max(0, dx - 12);
        int y = options.Position == "Custom" ? (int)Math.Round(dy * options.Y) : options.Position.StartsWith("Bottom", StringComparison.Ordinal) ? Math.Max(0, dy - 12) : Math.Min(12, dy);
        return new(work.Left + x, work.Top + y, work.Left + x + width, work.Top + y + height);
    }
}
