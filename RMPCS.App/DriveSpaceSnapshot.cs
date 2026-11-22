using System.IO;
using RMPCS.Core;
namespace RMPCS.App;

internal static class DriveSpaceSnapshot
{
    public static IReadOnlyList<DriveSpace> Capture()
    {
        var values = new List<DriveSpace>();
        try
        {
            // Observe the OS drive only; avoid probing removable or network volumes during maintenance.
            string root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
            try { var drive = new DriveInfo(root); values.Add(new(root, drive.AvailableFreeSpace)); }
            catch { values.Add(new(root, null)); }
        }
        catch { }
        return values;
    }
}
