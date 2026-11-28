using System.Runtime.InteropServices;
// Every native import in this app is a Windows system library. Never search the portable app folder for DLLs.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace RMPCS.App;
internal static class NativeSearchPolicy
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize()
    {
        // Also covers reviewed vendor code that calls LoadLibrary directly. Framework native
        // libraries use their runtime-provided absolute locations; app/current folders are excluded.
        if (!SetDefaultDllDirectories(0x800)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetDefaultDllDirectories(uint flags);
}
