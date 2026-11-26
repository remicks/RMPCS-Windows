using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using RMPCS.Core;

namespace RMPCS.App;

internal static class TaskSchedulerService
{
    private static string Sid => WindowsIdentity.GetCurrent().User!.Value;
    private static string Name(bool startup) => "DegeneratesInc.RMPCS." + (startup ? "Startup." : "Maintenance.") + Sid;
    public static bool IsInstalled => string.Equals(Path.GetFullPath(Environment.ProcessPath ?? ""), TaskDefinition.InstalledExe, StringComparison.OrdinalIgnoreCase) && SafePath(TaskDefinition.InstalledExe);

    private static bool SafePath(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }

    private static T WithFolder<T>(Func<dynamic, T> action)
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!)!;
        object? folder = null;
        try { service.Connect(); folder = service.GetFolder("\\"); return action(folder); }
        finally { if (folder is not null) Marshal.FinalReleaseComObject(folder); Marshal.FinalReleaseComObject(service); }
    }
    public static void Save(bool startup, ScheduleOptions? options = null)
    {
        if (!IsInstalled) throw new InvalidOperationException("Install RMPCS with the MSI before enabling startup or schedules. Elevated automatic tasks must run from the protected installation folder.");
        var xml = TaskDefinition.Build(Sid, TaskDefinition.InstalledExe, startup, options);
        WithFolder(folder =>
        {
            // Create/update, current interactive user, no password storage.
            object task = folder.RegisterTask(Name(startup), xml, 6, Sid, null, 3, $"D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FRFX;;;{Sid})");
            Marshal.FinalReleaseComObject(task);
            return true;
        });
    }
    public static void Remove(bool startup) => WithFolder(folder =>
    {
        try { folder.DeleteTask(Name(startup), 0); }
        catch (COMException ex) when ((uint)ex.HResult == 0x80070002) { }
        return true;
    });
    public static (string Status, string? Xml) Read(bool startup) => WithFolder(folder =>
    {
        object? task = null;
        try
        {
            task = folder.GetTask(Name(startup));
            dynamic registered = task;
            DateTime next = registered.NextRunTime;
            string state = registered.Enabled ? "Enabled" : "Disabled";
            string status = startup ? state + " at sign-in" : state + (next.Year > 2000 ? $" · Next: {next:g}" : " · Next run depends on logon/idle conditions");
            return (status, (string?)registered.Xml);
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x80070002) { return ("Off", (string?)null); }
        finally { if (task is not null) Marshal.FinalReleaseComObject(task); }
    });

    // Any task shape RMPCS did not write (edited in Task Scheduler, older/newer layout) is reported
    // as one InvalidDataException so callers can keep the known on/off state usable.
    public static ScheduleOptions ParseOptions(string xml)
    {
        try { return ParseOwnedOptions(xml); }
        catch (Exception ex) when (ex is not InvalidDataException) { throw new InvalidDataException("The saved schedule does not match the layout RMPCS creates.", ex); }
    }
    private static ScheduleOptions ParseOwnedOptions(string xml)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var task = XDocument.Parse(xml);
        var trigger = task.Descendants(ns + "CalendarTrigger").Single();
        var time = DateTime.Parse(trigger.Element(ns + "StartBoundary")!.Value, System.Globalization.CultureInfo.InvariantCulture);
        var week = trigger.Element(ns + "ScheduleByWeek");
        DayOfWeek day = week is null ? DayOfWeek.Sunday : Enum.Parse<DayOfWeek>(week.Element(ns + "DaysOfWeek")!.Elements().Single().Name.LocalName);
        if (!ScheduleLaunch.TryParse(task.Descendants(ns + "Arguments").Single().Value.Split(' ', StringSplitOptions.RemoveEmptyEntries), out var launch)) throw new InvalidOperationException("Unsupported scheduled profile arguments.");
        return new ScheduleOptions(week is null ? ScheduleFrequency.Daily : ScheduleFrequency.Weekly, day, time.Hour, time.Minute,
            launch.Repair,
            task.Descendants(ns + "RunOnlyIfIdle").Single().Value == "true",
            task.Descendants(ns + "DisallowStartIfOnBatteries").Single().Value == "true") { StartWindowMinutes = launch.WindowMinutes, WorkloadDeferMinutes = launch.DeferMinutes };
    }
}
