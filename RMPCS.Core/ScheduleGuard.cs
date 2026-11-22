using System.Globalization;
namespace RMPCS.Core;

public sealed record ScheduleLaunch(bool Repair, int Hour = 0, int Minute = 0, int WindowMinutes = 0, int DeferMinutes = 0, DayOfWeek? Day = null, bool RequireIdle = false, bool RequireAc = false)
{
    public static bool TryParse(string[] args, out ScheduleLaunch launch)
    {
        launch = null!;
        if (args.Length == 0 || args[0] is not ("--scheduled-light" or "--scheduled-repair")) return false;
        bool repair = args[0] == "--scheduled-repair";
        if (args.Length == 1) { launch = new(repair); return true; }
        if (args.Length != 7 || !args[1].StartsWith("--start=", StringComparison.Ordinal) || !TimeOnly.TryParseExact(args[1][8..], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !args[2].StartsWith("--window=", StringComparison.Ordinal) || !int.TryParse(args[2][9..], NumberStyles.None, CultureInfo.InvariantCulture, out int window) || window is not (0 or 60 or 120 or 240) ||
            !args[3].StartsWith("--defer=", StringComparison.Ordinal) || !int.TryParse(args[3][8..], NumberStyles.None, CultureInfo.InvariantCulture, out int defer) || defer is not (0 or 15 or 30 or 60) || !args[4].StartsWith("--day=", StringComparison.Ordinal)) return false;
        string dayText = args[4][6..]; DayOfWeek? day = null;
        if (dayText != "any") { if (!Enum.TryParse<DayOfWeek>(dayText, false, out var value) || !Enum.IsDefined(value) || value.ToString() != dayText) return false; day = value; }
        if (args[5] is not ("--idle=0" or "--idle=1") || args[6] is not ("--ac=0" or "--ac=1")) return false;
        launch = new(repair, start.Hour, start.Minute, window, defer, day, args[5] == "--idle=1", args[6] == "--ac=1"); return true;
    }
    public string Arguments => (Repair ? "--scheduled-repair" : "--scheduled-light") + (WindowMinutes == 0 && DeferMinutes == 0 ? "" : $" --start={Hour:00}:{Minute:00} --window={WindowMinutes} --defer={DeferMinutes} --day={Day?.ToString() ?? "any"} --idle={(RequireIdle ? 1 : 0)} --ac={(RequireAc ? 1 : 0)}");
    public bool InWindow(DateTime now)
    {
        if (WindowMinutes == 0) return true;
        var start = now.Date.AddHours(Hour).AddMinutes(Minute); if (now < start) start = start.AddDays(-1);
        return (Day is null || start.DayOfWeek == Day) && now >= start && now < start.AddMinutes(WindowMinutes);
    }
}
public sealed record WorkloadSample(double? CpuPercent, double? MemoryPercent, double? TrafficMiBPerSecond, bool? FullscreenOrPresentation, bool? CaptureActive, bool? AcPower = null, TimeSpan? IdleFor = null);
public static class WorkloadRules
{
    public static string? DeferReason(WorkloadSample sample, bool requireIdle = false, bool requireAc = false)
    {
        if (requireAc && sample.AcPower != true) return "AC power is unavailable or unknown.";
        if (requireIdle && (sample.IdleFor is null || sample.IdleFor < TimeSpan.FromMinutes(10))) return "Ten minutes of input idle time has not been verified.";
        if (sample.CpuPercent is null || sample.MemoryPercent is null || sample.TrafficMiBPerSecond is null || sample.FullscreenOrPresentation is null || sample.CaptureActive is null) return "A workload signal is unavailable.";
        if (!double.IsFinite(sample.CpuPercent.Value) || !double.IsFinite(sample.MemoryPercent.Value) || !double.IsFinite(sample.TrafficMiBPerSecond.Value) || sample.CpuPercent is < 0 or > 100 || sample.MemoryPercent is < 0 or > 100 || sample.TrafficMiBPerSecond < 0) return "A workload signal is invalid.";
        if (sample.FullscreenOrPresentation == true) return "Windows reports a full-screen/presentation or quiet state.";
        if (sample.CaptureActive == true) return "A microphone capture session is active (possibly a call or recording).";
        if (sample.CpuPercent >= 70) return "CPU use is at least 70%.";
        if (sample.MemoryPercent >= 85) return "RAM use is at least 85%.";
        if (sample.TrafficMiBPerSecond >= 5) return "Network traffic is at least 5 MiB/s (possibly downloads/uploads).";
        return null;
    }
}
