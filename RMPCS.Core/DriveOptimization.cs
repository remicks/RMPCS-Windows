namespace RMPCS.Core;

public enum DriveMedium { Unknown, HardDisk, SolidState }
public enum DriveAction { Analyze, Defragment, ReTrim }
public sealed record DriveCandidate(string Root, string VolumePath, DriveMedium Medium, bool TrimSupported,
    bool Eligible, ulong Capacity, ulong Free, string Identity, string Note)
{
    public string Label { get; init; } = "";
    public string Display => FriendlyNames.Drive(this);
}
public sealed record DriveAnalysis(string Identity, DateTimeOffset Time);
public sealed record DriveAvailability(bool Analyze, bool Optimize, string Label, string Reason);

public static class DriveOptimization
{
    // Only direct ATA/SATA/NVMe storage is supported in this first version. Controllers that
    // hide topology, removable media and contradictory/absent descriptors fail closed.
    public static DriveMedium Classify(int bus, bool removable, bool? seekPenalty, bool? trim) =>
        removable || bus is not (3 or 11 or 17) ? DriveMedium.Unknown :
        seekPenalty == false && trim == true ? DriveMedium.SolidState :
        bus is 3 or 11 && seekPenalty == true && trim == false ? DriveMedium.HardDisk : DriveMedium.Unknown;

    public static bool ValidVolume(string value) => value.Length == 49 && value.StartsWith(@"\\?\Volume{", StringComparison.Ordinal) &&
        value.EndsWith(@"}\", StringComparison.Ordinal) && Guid.TryParseExact(value.Substring(11, 36), "D", out var guid) && guid != Guid.Empty;
    public static bool ValidRoot(string value) => value.Length == 3 && value[0] is >= 'A' and <= 'Z' && value[1..] == @":\";
    public static DriveAvailability Availability(DriveCandidate drive)
    {
        if (!ValidRoot(drive.Root) || !ValidVolume(drive.VolumePath) || drive.Identity.Length == 0 || !drive.Eligible || drive.Capacity == 0 || drive.Free > drive.Capacity)
            return new(false, false, "Optimize selected drive", "Storage identity, layout or filesystem checks did not pass. " + drive.Note);
        if (drive.Medium is not (DriveMedium.HardDisk or DriveMedium.SolidState) || drive.Medium == DriveMedium.SolidState && !drive.TrimSupported || drive.Medium == DriveMedium.HardDisk && drive.TrimSupported)
            return new(false, false, "Optimization unavailable", "Media type and supported operation could not be verified. No drive operation will run.");
        if (drive.Medium == DriveMedium.HardDisk)
            return new(true, (double)drive.Free / drive.Capacity >= .15, "Defragment HDD", (double)drive.Free / drive.Capacity < .15
                ? "At least 15% free space is required by RMPCS for HDD defragmentation. Analysis is still available."
                : "Analyze first and review Windows' report before choosing defragmentation.");
        return new(true, true, "ReTRIM SSD", "Only ReTRIM will be requested. Traditional defragmentation is never requested on this SSD.");
    }
    public static string[] Arguments(DriveCandidate drive, DriveAction action)
    {
        var allowed = Availability(drive);
        bool valid = action switch { DriveAction.Analyze => allowed.Analyze, DriveAction.Defragment => allowed.Optimize && drive.Medium == DriveMedium.HardDisk && !drive.TrimSupported,
            DriveAction.ReTrim => allowed.Optimize && drive.Medium == DriveMedium.SolidState && drive.TrimSupported, _ => false };
        if (!valid) throw new InvalidOperationException("The requested operation is not permitted for this drive.");
        return [drive.VolumePath, action switch { DriveAction.Analyze => "/A", DriveAction.Defragment => "/D", _ => "/L" }, "/U", "/V"];
    }
    public static void Authorize(DriveCandidate expected, DriveCandidate current, DriveAction action, DriveAnalysis? analysis, bool? acPower, DateTimeOffset now)
    {
        if (expected.Root != current.Root || expected.VolumePath != current.VolumePath || expected.Identity != current.Identity || expected.Medium != current.Medium || expected.TrimSupported != current.TrimSupported)
            throw new InvalidOperationException("The drive changed since discovery. Refresh the drive list and review it again.");
        _ = Arguments(current, action);
        if (action != DriveAction.Analyze && acPower != true) throw new InvalidOperationException("Connect AC power before optimizing. Unavailable power status also blocks optimization.");
        if (action == DriveAction.Defragment && (analysis is null || analysis.Identity != current.Identity || analysis.Time > now || now - analysis.Time > TimeSpan.FromMinutes(15)))
            throw new InvalidOperationException("Analyze this HDD successfully within the last 15 minutes before defragmenting it.");
    }
}
