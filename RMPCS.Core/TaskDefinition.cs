using System.Xml.Linq;

namespace RMPCS.Core;

public enum ScheduleFrequency { Daily, Weekly }
public sealed record ScheduleOptions(ScheduleFrequency Frequency, DayOfWeek Day, int Hour, int Minute, bool Repair, bool RequireIdle, bool RequireAc)
{
    public int StartWindowMinutes { get; init; }
    public int WorkloadDeferMinutes { get; init; }
}

public static class TaskDefinition
{
    public static string InstalledExe => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Degenerates Inc", "RMPCS", "RMPCS.exe");
    public static string Build(string sid, string executable, bool startup, ScheduleOptions? options = null)
    {
        if (!sid.StartsWith("S-1-", StringComparison.Ordinal) || sid.Any(c => !char.IsAsciiDigit(c) && c != '-' && c != 'S')) throw new ArgumentException("Invalid Windows SID.");
        if (!Path.IsPathFullyQualified(executable)) throw new ArgumentException("An absolute executable path is required.");
        if (!startup && options is null) throw new ArgumentException("Schedule options required.");
        if (options is not null && (options.Hour is < 0 or > 23 || options.Minute is < 0 or > 59 || !Enum.IsDefined(options.Day) || !Enum.IsDefined(options.Frequency))) throw new ArgumentException("Invalid schedule time.");
        if (options is not null && (options.StartWindowMinutes is not (0 or 60 or 120 or 240) || options.WorkloadDeferMinutes is not (0 or 15 or 30 or 60))) throw new ArgumentException("Invalid start safeguard limits.");
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name, object? content) => new(ns + name, content);
        XElement trigger;
        if (startup) trigger = E("LogonTrigger", new[] { E("Enabled", "true"), E("UserId", sid), E("Delay", "PT30S") });
        else
        {
            // Local wall-clock boundary (no offset) follows the PC's local timezone.
            var time = $"2026-01-01T{options!.Hour:00}:{options.Minute:00}:00";
            trigger = E("CalendarTrigger", new[] { E("StartBoundary", time), E("Enabled", "true"),
                options.Frequency == ScheduleFrequency.Daily ? E("ScheduleByDay", E("DaysInterval", 1)) :
                    E("ScheduleByWeek", new[] { E("WeeksInterval", 1), E("DaysOfWeek", E(options.Day.ToString(), null)) }) });
        }
        var settings = E("Settings", new[] {
            E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", !startup && options!.RequireAc ? "true" : "false"),
            E("StopIfGoingOnBatteries", "false"), E("AllowHardTerminate", "false"), E("StartWhenAvailable", startup ? "false" : "true"),
            E("RunOnlyIfNetworkAvailable", "false"),
            E("IdleSettings", new[] { E("Duration", "PT10M"), E("WaitTimeout", "PT1H"), E("StopOnIdleEnd", "false"), E("RestartOnIdle", "false") }),
            E("AllowStartOnDemand", "true"), E("Enabled", "true"), E("Hidden", "false"), E("RunOnlyIfIdle", !startup && options!.RequireIdle ? "true" : "false"),
            E("WakeToRun", "false"), E("ExecutionTimeLimit", "PT0S"), E("Priority", 7) });
        var root = new XElement(ns + "Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", E("Description", startup ? "Open RMPCS at sign-in. No automatic maintenance." : "RMPCS authorized scheduled care. No advanced cleanup, DHCP reset or reboot.")),
            E("Triggers", trigger),
            E("Principals", new XElement(ns + "Principal", new XAttribute("id", "Owner"), E("UserId", sid), E("LogonType", "InteractiveToken"), E("RunLevel", "HighestAvailable"))),
            settings,
            new XElement(ns + "Actions", new XAttribute("Context", "Owner"), E("Exec", new[] { E("Command", executable),
                E("Arguments", startup ? "--startup" : new ScheduleLaunch(options!.Repair, options.Hour, options.Minute, options.StartWindowMinutes, options.WorkloadDeferMinutes, options.Frequency == ScheduleFrequency.Weekly ? options.Day : null, options.RequireIdle, options.RequireAc).Arguments), E("WorkingDirectory", Path.GetDirectoryName(executable)) })));
        return root.ToString();
    }
}
