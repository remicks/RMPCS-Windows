using System.Xml.Linq;
using RMPCS.Core;

internal static class WorkflowChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var platform = new FakeOperations();
        var result = await MaintenanceRunner.RunAsync(MaintenancePlan.FullBatch, platform, _ => { });
        check(platform.Events.SequenceEqual(new[] { "UserTemp", "WindowsTemp", "Prefetch", "WindowsLogs", "FlushDns", "RepairImage", "ScanSystemFiles", "ResetSearch", "ReleaseDhcp", "RenewDhcp", "Restart60" }), "full-batch plan covers every task category in controlled order");
        check(result.Failed == 0 && result.RestartScheduled, "successful selected full batch schedules the requested reboot");
        platform = new FakeOperations { ImageExit = 5 };
        result = await MaintenanceRunner.RunAsync(MaintenancePlan.Care with { Restart = true }, platform, _ => { });
        check(result.Failed == 2 && !platform.Events.Contains("ScanSystemFiles") && !result.RestartScheduled, "DISM failure skips SFC and suppresses reboot");
        platform = new FakeOperations { ThrowRelease = true };
        result = await MaintenanceRunner.RunAsync(new() { RenewDhcp = true }, platform, _ => { });
        check(result.Failed == 1 && platform.Events.SequenceEqual(new[] { "ReleaseDhcp", "RenewDhcp" }), "renewal is attempted even when release throws");
        bool stopped = false;
        platform = new FakeOperations { OnCommand = c => { if (c == SystemCommand.ReleaseDhcp) stopped = true; } };
        result = await MaintenanceRunner.RunAsync(new() { RenewDhcp = true, Restart = true }, platform, _ => { }, () => stopped);
        check(result.Stopped && platform.Events.SequenceEqual(new[] { "ReleaseDhcp", "RenewDhcp" }), "stop after release still renews and suppresses reboot");
        platform = new FakeOperations { ThrowCleanup = true };
        result = await MaintenanceRunner.RunAsync(MaintenancePlan.Light, platform, _ => { });
        check(result.Failed == 2 && platform.Events.Last() == "FlushDns", "cleanup exceptions are reported without skipping later independent work");
        platform = new FakeOperations { ThrowSearch = true };
        result = await MaintenanceRunner.RunAsync(new() { ResetSearch = true, Restart = true }, platform, _ => { });
        check(result.Failed == 1 && !result.RestartScheduled, "index-reset failure suppresses reboot");
        platform = new FakeOperations();
        await MaintenanceRunner.RunAsync(MaintenancePlan.Light, platform, _ => { });
        check(platform.Events.SequenceEqual(new[] { "UserTemp", "WindowsTemp", "FlushDns" }), "scheduled light profile excludes advanced and disruptive tasks");
        check(!MaintenancePlan.Care.Prefetch && !MaintenancePlan.Care.WindowsLogs && !MaintenancePlan.Care.ResetSearch && !MaintenancePlan.Care.RenewDhcp && !MaintenancePlan.Care.Restart, "scheduled repair profile excludes advanced and disruptive tasks");
        bool bad = false;
        try { (MaintenancePlan.Light with { MinimumAgeDays = -1 }).Validate(); } catch (ArgumentException) { bad = true; }
        check(bad, "invalid cleanup age rejected");
        string sid = "S-1-5-21-100-200-300-1001", executable = @"C:\Program Files\Degenerates & Inc\RMPCS\RMPCS.exe";
        var options = new ScheduleOptions(ScheduleFrequency.Weekly, DayOfWeek.Friday, 19, 30, true, true, true);
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var weekly = XDocument.Parse(TaskDefinition.Build(sid, executable, false, options));
        string Value(XDocument document, string key) => document.Descendants(ns + key).Single().Value;
        check(weekly.Descendants(ns + "DaysOfWeek").Single().Element(ns + "Friday") is not null && Value(weekly, "StartBoundary").EndsWith("T19:30:00"), "weekly day and local time encoded correctly");
        check(Value(weekly, "UserId") == sid && Value(weekly, "LogonType") == "InteractiveToken" && Value(weekly, "RunLevel") == "HighestAvailable", "task uses the consenting user's interactive elevated token");
        check(Value(weekly, "Command") == executable && Value(weekly, "Arguments") == "--scheduled-repair", "XML escaping preserves the executable path and fixed profile argument");
        check(Value(weekly, "AllowHardTerminate") == "false" && Value(weekly, "ExecutionTimeLimit") == "PT0S" && Value(weekly, "StopOnIdleEnd") == "false" && Value(weekly, "StopIfGoingOnBatteries") == "false", "task does not kill active repairs on timeout, activity or battery changes");
        check(Value(weekly, "RunOnlyIfIdle") == "true" && Value(weekly, "DisallowStartIfOnBatteries") == "true" && Value(weekly, "WakeToRun") == "false" && Value(weekly, "MultipleInstancesPolicy") == "IgnoreNew", "idle, AC, no-wake and overlap controls encoded");
        var daily = XDocument.Parse(TaskDefinition.Build(sid, executable, false, options with { Frequency = ScheduleFrequency.Daily, Repair = false, RequireIdle = false, RequireAc = false }));
        check(Value(daily, "DaysInterval") == "1" && Value(daily, "Arguments") == "--scheduled-light" && Value(daily, "RunOnlyIfIdle") == "false", "daily light schedule supports optional idle and AC conditions");
        var startup = XDocument.Parse(TaskDefinition.Build(sid, executable, true));
        check(startup.Descendants(ns + "LogonTrigger").Count() == 1 && Value(startup, "Arguments") == "--startup" && !startup.Descendants(ns + "CalendarTrigger").Any(), "autostart opens the app without authorizing maintenance");
        bad = false; try { _ = TaskDefinition.Build("malicious<sid>", executable, true); } catch (ArgumentException) { bad = true; }
        check(bad, "malformed task principal rejected");
        bad = false; try { _ = TaskDefinition.Build(sid, executable, false, options with { Hour = 25 }); } catch (ArgumentException) { bad = true; }
        check(bad, "invalid scheduled time rejected");
    }
    private sealed class FakeOperations : IMaintenanceOperations
    {
        public List<string> Events { get; } = [];
        public int ImageExit { get; init; }
        public bool ThrowRelease { get; init; }
        public bool ThrowCleanup { get; init; }
        public bool ThrowSearch { get; init; }
        public Action<SystemCommand>? OnCommand { get; init; }
        public Task<CleanupResult> CleanAsync(CleanupArea area, int minimumAgeDays)
        {
            Events.Add(area.ToString()); if (ThrowCleanup) throw new IOException("Fixture cleanup error"); return Task.FromResult(new CleanupResult(1, 12, 0));
        }
        public Task<int> RunAsync(SystemCommand command)
        {
            Events.Add(command.ToString()); OnCommand?.Invoke(command);
            if (command == SystemCommand.ReleaseDhcp && ThrowRelease) throw new IOException("Fixture release error");
            return Task.FromResult(command == SystemCommand.RepairImage ? ImageExit : 0);
        }
        public Task ResetSearchAsync() { Events.Add("ResetSearch"); if (ThrowSearch) throw new IOException("Fixture search error"); return Task.CompletedTask; }
    }
}
