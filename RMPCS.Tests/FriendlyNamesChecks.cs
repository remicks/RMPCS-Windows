using RMPCS.Core;
internal static class FriendlyNamesChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var drive=new DriveCandidate(@"D:\",@"\\?\Volume{12345678-1234-1234-1234-123456789abc}\",DriveMedium.SolidState,true,true,1000,500,"fixture",""){Label="Games"};
        check(drive.Display=="Games (D:) · SSD","drive choices combine a volume label, drive letter and verified media type");
        check((drive with{Label=""}).Display=="Local drive (D:) · SSD","unlabelled drives retain a clear drive letter fallback");
        check((drive with{Label="\nGames\u202e\t"}).Display==drive.Display&&FriendlyNames.CleanLabel(new string('x',300)).Length==64,"untrusted labels are bounded and remove controls and directional overrides");
        check(FriendlyNames.CleanLabel("Ga\u2028me\u2029s")=="Games","Unicode line separators cannot push the identifying drive letter onto a hidden line");
        check((drive with{Root=@"E:\"}).Display!=drive.Display,"duplicate labels remain distinguishable by drive letter");
        check((drive with{Medium=DriveMedium.Unknown}).Display.Contains("Type not verified"),"unverified media is never presented as a safe SSD");
        check(FriendlyNames.DriveAction(DriveMedium.SolidState,false)=="Optimize SSD"&&FriendlyNames.DriveAction(DriveMedium.SolidState,true).Contains("ReTRIM"),"technical drive-action names expand without changing the operation");
        check(DriveOptimization.Arguments(drive,DriveAction.ReTrim).SequenceEqual(DriveOptimization.Arguments(drive with{Label="Other name"},DriveAction.ReTrim)),"labels never enter drive command arguments");
        DriveOptimization.Authorize(drive,drive with{Label="Renamed"},DriveAction.ReTrim,null,true,DateTimeOffset.UtcNow);
        check(!DriveOptimization.Availability(drive with{Label="Verified SSD",Eligible=false}).Optimize,"a reassuring label cannot bypass safety checks");
        var simple=MaintenancePlan.Care.Describe();var technical=MaintenancePlan.Care.Describe(true);
        check(simple.Contains("My temporary files")&&!simple.Contains("UserTemp")&&!simple.Contains("DISM")&&technical.Contains("DISM")&&technical.Contains("SFC"),"task confirmations use friendly names with optional technical terms");
        check(MaintenancePlan.FullBatch.Describe().Contains("Disconnect")&&MaintenancePlan.FullBatch.Describe().Contains("Restart"),"disconnection and restart stay explicit in simplified confirmations");
        var defaults=AppPreferences.Parse("{}");var expanded=defaults with{ShowTechnicalNames=true,ExpandTechnicalDetails=true};
        check(!defaults.ShowTechnicalNames&&!defaults.ExpandTechnicalDetails&&AppPreferences.Parse(expanded.ToJson())==expanded&&new PreferenceUndo(defaults,expanded).Restore(expanded)==defaults,"information preferences default to simple, round-trip and support undo");
    }
}
