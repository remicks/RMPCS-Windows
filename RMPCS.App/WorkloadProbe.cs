using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using RMPCS.Core;
namespace RMPCS.App;

internal static class WorkloadProbe
{
    public static async Task<WorkloadSample> SampleAsync()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        bool times = GetSystemTimes(out ulong idle0, out ulong kernel0, out ulong user0); long? traffic0 = Traffic();
        await Task.Delay(1000);
        double? cpu = null, traffic = null; var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (times && GetSystemTimes(out ulong idle1, out ulong kernel1, out ulong user1) && idle1 >= idle0 && kernel1 >= kernel0 && user1 >= user0)
        { double total = (double)(kernel1 - kernel0) + (user1 - user0); if (total > 0) cpu = Math.Clamp(100d * (1 - (idle1 - idle0) / total), 0, 100); }
        long? traffic1 = Traffic(); if (traffic0 is long a && traffic1 is long b && b >= a && clock.Elapsed.TotalSeconds > 0) traffic = (b - a) / 1048576d / clock.Elapsed.TotalSeconds;
        bool? screen = SHQueryUserNotificationState(out int state) == 0 ? state is 2 or 3 or 4 or 6 or 7 ? true : state is 1 or 5 ? false : null : null;
        bool? capture = await Task.Run(CaptureActive);
        var input = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        TimeSpan? idle = GetLastInputInfo(ref input) ? TimeSpan.FromMilliseconds(unchecked(GetTickCount() - input.Tick)) : null;
        bool? ac = GetSystemPowerStatus(out var power) ? power.Ac == 1 ? true : power.Ac == 0 ? false : null : null;
        return new(cpu, GlobalMemoryStatusEx(ref memory) ? memory.Load : null, traffic, screen, capture, ac, idle);
    }
    private static long? Traffic()
    {
        try { long total = 0; foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))) { var stats = adapter.GetIPStatistics(); total = checked(total + stats.BytesReceived + stats.BytesSent); } return total; }
        catch { return null; }
    }
    // Read capture-session state only. Never opens an audio capture stream or records sound.
    private static bool? CaptureActive()
    {
        object? enumeratorObject = null; DeviceCollection? devices = null;
        try
        {
            enumeratorObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), true)!);
            Marshal.ThrowExceptionForHR(((DeviceEnumerator)enumeratorObject!).EnumAudioEndpoints(1, 1, out devices));
            Marshal.ThrowExceptionForHR(devices.GetCount(out uint count)); if (count > 8) return null;
            for (uint d = 0; d < count; d++)
            {
                Device? device = null; object? managerObject = null; SessionEnumerator? sessions = null;
                try
                {
                    Marshal.ThrowExceptionForHR(devices.Item(d, out device));
                    Guid managerId = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"); // IAudioSessionManager2
                    Marshal.ThrowExceptionForHR(device.Activate(ref managerId, 23, IntPtr.Zero, out managerObject));
                    Marshal.ThrowExceptionForHR(((SessionManager)managerObject).GetSessionEnumerator(out sessions));
                    Marshal.ThrowExceptionForHR(sessions.GetCount(out int sessionCount)); if (sessionCount is < 0 or > 64) return null;
                    for (int i = 0; i < sessionCount; i++)
                    {
                        SessionControl? session = null;
                        try { Marshal.ThrowExceptionForHR(sessions.GetSession(i, out session)); Marshal.ThrowExceptionForHR(session.GetState(out int status)); if (status == 1) return true; }
                        finally { Release(session); }
                    }
                }
                finally { Release(sessions); Release(managerObject); Release(device); }
            }
            return false;
        }
        catch { return null; }
        finally { Release(devices); Release(enumeratorObject); }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) try { Marshal.FinalReleaseComObject(value); } catch { } }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface DeviceEnumerator { [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out DeviceCollection devices); }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-C0A826EC6A0A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface DeviceCollection { [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index, out Device device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface Device { [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result); }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface SessionManager
    { [PreserveSig] int GetAudioSessionControl(IntPtr guid, uint flags, out IntPtr control); [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, uint flags, out IntPtr volume); [PreserveSig] int GetSessionEnumerator(out SessionEnumerator sessions); }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface SessionEnumerator { [PreserveSig] int GetCount(out int count); [PreserveSig] int GetSession(int index, out SessionControl session); }
    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface SessionControl { [PreserveSig] int GetState(out int state); }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Tick; }
    [StructLayout(LayoutKind.Sequential)] private struct PowerStatus { public byte Ac, Flags, Percent, Reserved; public uint Life, FullLife; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput input);
    [DllImport("kernel32.dll")] private static extern uint GetTickCount();
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out PowerStatus status);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
}
