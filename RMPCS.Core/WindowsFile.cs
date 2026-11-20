using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace RMPCS.Core;

internal static class WindowsFile
{
    // Validate and delete through the same handle so a renamed/replaced path cannot
    // redirect an elevated cleanup to a different file after validation.
    public static long? DeleteEligible(string path, string root, DateTime cutoff)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var handle = CreateFileW(path, 0x00010080, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid) throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
        var final = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandleW(handle, final, (uint)final.Capacity, 0);
        if (length == 0 || length >= final.Capacity) throw new IOException("Cannot verify final cleanup path.");
        var resolved = final.ToString();
        if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) resolved = @"\\" + resolved[8..];
        else if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) resolved = resolved[4..];
        if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
        if (!GetFileInformationByHandle(handle, out var info)) throw new IOException("Cannot read cleanup file information.");
        const uint excluded = (uint)(FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.ReadOnly);
        // Hard links can name files elsewhere; skip them rather than changing shared data.
        if ((info.Attributes & excluded) != 0 || info.Links != 1) return null;
        if (ToDateTime(info.Created) >= cutoff || ToDateTime(info.Written) >= cutoff) return null;
        var disposition = new Disposition { Delete = true };
        if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf<Disposition>()))
            throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
        return ((long)info.SizeHigh << 32) | info.SizeLow;
    }

    private static DateTime ToDateTime(System.Runtime.InteropServices.ComTypes.FILETIME time) =>
        DateTime.FromFileTimeUtc(((long)(uint)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct Information
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Disposition { [MarshalAs(UnmanagedType.Bool)] public bool Delete; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out Information info);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int infoClass, ref Disposition info, uint size);
}
