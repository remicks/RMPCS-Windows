namespace RMPCS.Core;
public static class FriendlyNames
{
    public static string CleanLabel(string? label) => new string((label??"").Where(c=>!char.IsControl(c)&&char.GetUnicodeCategory(c) is not (System.Globalization.UnicodeCategory.Format or System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator)).Take(64).ToArray()).Trim();
    public static string Drive(DriveCandidate drive)
    {
        string label=CleanLabel(drive.Label),letter=DriveOptimization.ValidRoot(drive.Root)?drive.Root[..2]:"Drive";
        return $"{(label.Length==0?"Local drive":label)} ({letter}) · {drive.Medium switch{DriveMedium.SolidState=>"SSD",DriveMedium.HardDisk=>"Hard drive",_=>"Type not verified"}}";
    }
    public static string DriveAction(DriveMedium medium,bool technical) => medium switch
    {DriveMedium.SolidState=>technical?"Optimize SSD (ReTRIM)":"Optimize SSD",DriveMedium.HardDisk=>technical?"Optimize hard drive (defragment)":"Optimize hard drive",_=>"Optimization unavailable"};
    public static string Cleanup(CleanupArea area,bool technical=false)=>area switch
    {
        CleanupArea.UserTemp=>"My temporary files",CleanupArea.WindowsTemp=>"Windows temporary files",CleanupArea.Prefetch=>technical?"App startup cache (Prefetch .pf)":"App startup cache",
        CleanupArea.WindowsLogs=>"Windows troubleshooting logs",CleanupArea.Thumbnails=>"File preview cache",CleanupArea.ErrorArchives=>"Saved Windows error reports",CleanupArea.CrashDumps=>technical?"App crash files (.dmp)":"App crash files",CleanupArea.RecycleBin=>"Recycle Bin",_=>"Unknown cleanup task"
    };
}
