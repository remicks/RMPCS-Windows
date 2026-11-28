using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using RMPCS.Core;

namespace RMPCS.App;

// Local read-only counters. No sensors/kernel drivers, processes, commands, sockets or telemetry.
internal sealed class MonitorProbe
{
    private double diskTime = double.NegativeInfinity;
    private ulong? diskFree;
    private ulong? diskCapacity;
    internal MonitorSample Read(OverlayPreferences options)
    {
        double time = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        CpuTimes? cpu = options.Cpu && Environment.ProcessorCount <= 64 && GetSystemTimes(out ulong idle, out ulong kernel, out ulong user) ? new(idle, kernel, user) : null;
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        bool haveMemory = options.Memory && GlobalMemoryStatusEx(ref memory);
        Dictionary<string, NetworkBytes>? network = null;
        if (options.Network)
        {
            try
            {
                network = new(StringComparer.Ordinal);
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                    if (adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                    {
                        var stats = adapter.GetIPStatistics();
                        network.Add(adapter.Id, new(stats.BytesReceived, stats.BytesSent));
                    }
            }
            catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException or OverflowException or NotSupportedException or ArgumentException or System.Security.SecurityException) { network = null; }
        }
        if (options.DiskSpace && time - diskTime >= 30)
        {
            diskTime = time; diskFree = diskCapacity = null;
            string? root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
            if (root is not null && DriveOptimization.ValidRoot(root) && GetDiskFreeSpaceExW(root, out ulong available, out ulong capacity, out _)) { diskFree = available; diskCapacity = capacity; }
        }
        return new(time, cpu, haveMemory ? memory.TotalPhysical : null, haveMemory ? memory.AvailablePhysical : null,
            network, options.DiskSpace ? diskFree : null, options.Uptime ? Environment.TickCount64 : 0, options.DiskSpace ? diskCapacity : null);
    }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetDiskFreeSpaceExW(string path, out ulong available, out ulong capacity, out ulong free);
}
