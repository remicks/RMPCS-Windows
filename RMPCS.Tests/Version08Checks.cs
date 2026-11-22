using System.Xml.Linq;
using RMPCS.Core;
internal static class Version08Checks
{
    public static void Run(Action<bool,string> check)
    {
        bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        check(ScheduleLaunch.TryParse(["--scheduled-light"],out var legacy) && !legacy.Repair && legacy.DeferMinutes == 0 && legacy.WindowMinutes == 0, "legacy scheduled profiles keep safeguards off");
        var launch = new ScheduleLaunch(true,23,30,120,15,DayOfWeek.Sunday,true,true);
        check(ScheduleLaunch.TryParse(launch.Arguments.Split(' '),out var parsed) && parsed == launch, "guarded scheduled arguments round-trip fixed profile, window, day, idle and AC conditions");
        foreach (string invalid in new[] { launch.Arguments+" --extra", launch.Arguments.Replace("--start=23:30","--start=24:00"), launch.Arguments.Replace("--window=120","--window=1"), launch.Arguments.Replace("--defer=15","--defer=999"), launch.Arguments.Replace("--day=Sunday","--day=0"), launch.Arguments.Replace("--ac=1","--ac=true"), launch.Arguments.Replace("--scheduled-repair","--scheduled-anything"), launch.Arguments.Replace("--idle=1","--idle=1;calc") })
            check(!ScheduleLaunch.TryParse(invalid.Split(' '),out _), "scheduled grammar rejects unsupported/unsafe values: " + invalid);
        check(launch.InWindow(new DateTime(2026,11,23,0,30,0)) && !launch.InWindow(new DateTime(2026,11,24,0,30,0)), "weekly window crossing midnight belongs to its scheduled start day");
        check(!launch.InWindow(new DateTime(2026,11,22,23,29,59)) && launch.InWindow(new DateTime(2026,11,22,23,30,0)) && !launch.InWindow(new DateTime(2026,11,23,1,30,0)), "maintenance start window has exact inclusive start/exclusive end");
        check(new ScheduleLaunch(false,12,0,60).InWindow(new DateTime(2026,11,22,12,30,0)) && legacy.InWindow(DateTime.MinValue), "daily and unrestricted legacy window rules are distinct");
        var options = new ScheduleOptions(ScheduleFrequency.Weekly,DayOfWeek.Sunday,23,30,true,true,true) { StartWindowMinutes=120, WorkloadDeferMinutes=15 };
        string xml = TaskDefinition.Build("S-1-5-21-1",TaskDefinition.InstalledExe,false,options);
        XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";
        check(XDocument.Parse(xml).Descendants(ns+"Arguments").Single().Value==launch.Arguments && xml.Contains("<AllowHardTerminate>false</AllowHardTerminate>") && xml.Contains("<StopOnIdleEnd>false</StopOnIdleEnd>"), "scheduler XML carries guard limits without allowing active repairs to be terminated");
        check(Reject(()=>TaskDefinition.Build("S-1-5-21-1",TaskDefinition.InstalledExe,false,options with { WorkloadDeferMinutes=120 })), "scheduler rejects unbounded/non-supported wait values");
        var idle = new WorkloadSample(10,50,0.1,false,false,true,TimeSpan.FromMinutes(11));
        check(WorkloadRules.DeferReason(idle,true,true) is null, "available quiet workload allows a guarded start");
        check(WorkloadRules.DeferReason(idle with { CpuPercent=70 }) is not null && WorkloadRules.DeferReason(idle with { MemoryPercent=85 }) is not null && WorkloadRules.DeferReason(idle with { TrafficMiBPerSecond=5 }) is not null, "CPU, RAM and traffic thresholds defer at their boundaries");
        check(WorkloadRules.DeferReason(idle with { FullscreenOrPresentation=true }) is not null && WorkloadRules.DeferReason(idle with { CaptureActive=true }) is not null, "full-screen/presentation and microphone activity defer starts");
        check(WorkloadRules.DeferReason(idle with { CaptureActive=null }) is not null && WorkloadRules.DeferReason(idle with { CpuPercent=double.NaN }) is not null && WorkloadRules.DeferReason(idle with { TrafficMiBPerSecond=-1 }) is not null, "missing, nonfinite and invalid workload signals defer conservatively");
        check(WorkloadRules.DeferReason(idle with { AcPower=false },true,true) is not null && WorkloadRules.DeferReason(idle with { IdleFor=TimeSpan.Zero },true,true) is not null && WorkloadRules.DeferReason(idle with { AcPower=null },false,true) is not null, "idle and AC conditions are rechecked after workload deferral");
        var before = new AppPreferences(); var after = before with { RestoreBeforeRepair=true, StartPage="Protection" };
        check(RestoreVerification.IsNew(0,12,13) && RestoreVerification.IsNew(0,-1,1), "restore success requires a new matching sequence after the previous inventory");
        check(!RestoreVerification.IsNew(0,12,12) && !RestoreVerification.IsNew(0,12,-1) && !RestoreVerification.IsNew(5,12,13), "accepted-but-skipped restore requests and Windows failures are never reported as verified");
        var undo = PreferenceUndo.Parse(new PreferenceUndo(before,after).ToJson());
        check(undo.Restore(after)==before && !before.RestoreBeforeRepair, "undo restores only recorded app preferences; pre-repair restore defaults off");
        check(Reject(()=>undo.Restore(after with { ActivityFontSize=16 })), "undo refuses to overwrite a conflicting later preference state");
        check(Reject(()=>PreferenceUndo.Parse("{}")) && Reject(()=>PreferenceUndo.Parse(new string('x',16385))) && Reject(()=>PreferenceUndo.Parse("{\"Before\":null,\"After\":{}}")), "undo rejects incomplete and oversized records");
        check(new PreferenceUndo(before with { StartPage="untrusted",ActivityFontSize=100 },after).Restore(after).StartPage=="Health", "undo normalizes preferences instead of accepting arbitrary UI values");
        var events = new[] { new StabilityEvent(DateTime.Now,"System/Kernel\nInjected",41,1), new StabilityEvent(DateTime.Now,"System/Kernel\nInjected",41,1) };
        string report = StabilityReport.Format(events,7,[]);
        check(report.Contains("2 occurrence(s)") && report.Contains("metadata only") && !report.Contains("Kernel\nInjected") && report.Contains("do not prove"), "stability report groups recurring IDs, removes control characters and avoids causal claims");
        check(StabilityReport.Format([],1,["channel unavailable"]).Contains("channel unavailable") && StabilityReport.Format([],1,[]).Contains("does not prove stability"), "empty/unavailable event samples do not imply a stable PC");
        var sample = new NetworkSample(DateTimeOffset.Now,"github.com",true,true,"192.0.2.1",5,3,2.5,"fixture");
        check(NetworkDiagnosticPolicy.AllowedTarget("github.com") && NetworkDiagnosticPolicy.AllowedTarget("www.microsoft.com") && new[] { "localhost", "127.0.0.1", "github.com.evil.test", "user@github.com", "github.com:443", "https://github.com", "github.com\n" }.All(x=>!NetworkDiagnosticPolicy.AllowedTarget(x)), "network diagnostics accept only exact fixed public hostnames, not arbitrary targets or URL tricks");
        check(new[] { "127.0.0.1", "::1", "0.0.0.0", "::", "224.0.0.1", "255.255.255.255", "ff02::1", "::ffff:192.0.2.1" }.All(x=>!NetworkDiagnosticPolicy.UsableGateway(System.Net.IPAddress.Parse(x))), "gateway probes reject loopback, unspecified, multicast, broadcast and mapped addresses");
        check(NetworkDiagnosticPolicy.UsableGateway(System.Net.IPAddress.Parse("192.168.1.1")) && NetworkDiagnosticPolicy.UsableGateway(System.Net.IPAddress.Parse("fe80::1%1")), "OS-provided private IPv4 and scoped IPv6 gateway addresses remain usable");
        check(sample.LossPercent==40 && sample.Format().Contains("No network settings were changed") && sample.Advice.Contains("No network reset"), "network report computes gateway loss and avoids unnecessary reset advice");
        check(NetworkSample.Compare(sample,sample with { AverageMs=1 }).Contains("does not prove") && NetworkSample.Compare(sample,sample with { Gateway="192.0.2.2" }).Contains("not directly comparable") && NetworkSample.Compare(sample,sample with { Target="www.microsoft.com" }).Contains("not directly comparable"), "network comparison rejects changed routes/targets and avoids claiming causation");
        check((sample with { Sent=0,Replies=0,AverageMs=null }).LossPercent is null && (sample with { DnsOk=false }).Advice.Contains("does not establish a cause"), "unavailable gateway/DNS measurements remain explicit");
        check(CompletionNotices.Text(CompletionNotice.Deferred).Length>0 && AppPreferences.Pages.Contains("Diagnostics") && AppPreferences.Pages.Contains("Protection"), "deferred notices and new navigation destinations are available");
    }
}
