using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace RMPCS.App;

internal static class StartupBackupSecurity
{
    private const string Path = @"SOFTWARE\DegeneratesInc\RMPCS\DisabledStartup";
    private const string DescriptorText = "D:P(A;;KA;;;SY)(A;;KA;;;BA)";
    public static void Protect(bool create)
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        if (create) { using var created = machine.CreateSubKey(Path, true); }
        using var key = machine.OpenSubKey(Path, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ReadKey | RegistryRights.WriteKey | RegistryRights.ChangePermissions);
        if (key is null) return;
        // No inherited Users/Authenticated Users access: a current-user Run command
        // may contain private arguments even though machine-wide Run entries are public.
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(DescriptorText, 1, out var descriptor, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (!GetSecurityDescriptorDacl(descriptor, out bool present, out var dacl, out _) || !present || dacl == IntPtr.Zero)
                throw new InvalidOperationException("Cannot establish startup backup protections.");
            uint error = SetSecurityInfo(key.Handle, 4, 0x80000004, IntPtr.Zero, IntPtr.Zero, dacl, IntPtr.Zero);
            if (error != 0) throw new Win32Exception((int)error, "Startup backup protections could not be applied. No startup entry was changed.");
        }
        finally { LocalFree(descriptor); }
    }
    public static void VerifyDescriptor()
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(DescriptorText, 1, out var descriptor, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (!GetSecurityDescriptorControl(descriptor, out var control, out _) || (control & 0x1000) == 0) throw new InvalidOperationException("Backup ACL must block inheritance.");
            if (!GetSecurityDescriptorDacl(descriptor, out var present, out var dacl, out _) || !present || dacl == IntPtr.Zero) throw new InvalidOperationException("Backup DACL is missing.");
            if (!GetAclInformation(dacl, out var info, (uint)Marshal.SizeOf<AclInfo>(), 2) || info.Count != 2) throw new InvalidOperationException("Unexpected backup trustees.");
            var trustees = new HashSet<string>();
            for (uint i = 0; i < info.Count; i++)
            {
                if (!GetAce(dacl, i, out var ace) || Marshal.ReadByte(ace) != 0 || unchecked((uint)Marshal.ReadInt32(ace, 4)) != 0x000F003F) throw new InvalidOperationException("Unexpected backup access rule.");
                if (!ConvertSidToStringSidW(IntPtr.Add(ace, 8), out var sid)) throw new Win32Exception(Marshal.GetLastWin32Error());
                try { trustees.Add(Marshal.PtrToStringUni(sid)!); } finally { LocalFree(sid); }
            }
            if (!trustees.SetEquals(["S-1-5-18", "S-1-5-32-544"])) throw new InvalidOperationException("Only SYSTEM and Administrators may access backup data.");
        }
        finally { LocalFree(descriptor); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct AclInfo { public uint Count, Used, Free; }
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSecurityDescriptorControl(IntPtr descriptor, out ushort control, out uint revision);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetAclInformation(IntPtr acl, out AclInfo info, uint size, uint type);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetAce(IntPtr acl, uint index, out IntPtr ace);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr text);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorDacl(IntPtr descriptor, [MarshalAs(UnmanagedType.Bool)] out bool present, out IntPtr dacl, [MarshalAs(UnmanagedType.Bool)] out bool defaulted);
    [DllImport("advapi32.dll")] private static extern uint SetSecurityInfo(SafeRegistryHandle handle, uint type, uint information, IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
