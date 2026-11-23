using System.Runtime.InteropServices;
// Every native import in this app is a Windows system library. Never search the portable app folder for DLLs.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
