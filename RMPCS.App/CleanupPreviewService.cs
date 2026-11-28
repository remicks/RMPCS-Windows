using System.Runtime.InteropServices;
using RMPCS.Core;

namespace RMPCS.App;

internal sealed record CleanupPreviewRow(string Area, long Files, long Bytes, int Skipped, string Note, IReadOnlyList<string> Paths)
{ public string Megabytes => (Bytes / 1048576d).ToString("N2"); }
internal static class CleanupPreviewService
{
    public static CleanupPreviewRow Scan(CleanupArea area, int days,bool technical=false)
    {
        if (area == CleanupArea.RecycleBin)
        {
            var bin = QueryBin(); return new(FriendlyNames.Cleanup(area,technical), bin.Items, bin.Bytes, 0, "All ages; all drives for this Windows identity. Permanent if emptied.", []);
        }
        var preview = WindowsOperations.Cleaner(area, days).Scan(DateTime.UtcNow);
        return new(FriendlyNames.Cleanup(area,technical), preview.Files.Count, preview.Bytes, preview.Skipped,
            area is CleanupArea.ErrorArchives or CleanupArea.CrashDumps ? "Diagnostic evidence will be removed. Active queues are preserved." : "Locked/protected entries may be skipped when run.", preview.Files);
    }
    public static (long Items, long Bytes) QueryBin()
    {
        var info = new BinInfo { Size = (uint)Marshal.SizeOf<BinInfo>() };
        Marshal.ThrowExceptionForHR(SHQueryRecycleBinW(null, ref info)); return (Math.Max(0, info.Items), Math.Max(0, info.Bytes));
    }
    public static CleanupResult EmptyBin()
    {
        var before = QueryBin(); if (before.Items == 0) return new(0, 0, 0);
        Marshal.ThrowExceptionForHR(SHEmptyRecycleBinW(IntPtr.Zero, null, 7));
        var after = QueryBin();
        return new((int)Math.Min(int.MaxValue, Math.Max(0, before.Items - after.Items)), Math.Max(0, before.Bytes - after.Bytes), 0);
    }
    [StructLayout(LayoutKind.Sequential)] private struct BinInfo { public uint Size; public long Bytes, Items; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int SHQueryRecycleBinW(string? root, ref BinInfo info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int SHEmptyRecycleBinW(IntPtr window, string? root, uint flags);
}
