using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using RMPCS.Core;

namespace RMPCS.App;

internal static class DriveOptimizationService
{
    internal static IReadOnlyList<DriveCandidate> Discover()
    {
        var drives = new List<DriveCandidate>();
        foreach (var drive in DriveInfo.GetDrives().OrderBy(d => d.Name, StringComparer.Ordinal))
            if (drive.DriveType == DriveType.Fixed) drives.Add(Probe(drive.Name));
        return drives;
    }

    internal static DriveCandidate Probe(string root)
    {
        if (!DriveOptimization.ValidRoot(root)) throw new ArgumentException("Only a local drive-letter root is permitted.");
        string label="";
        try
        {
            if (GetDriveTypeW(root) != 3) throw new IOException("Only fixed local drives are supported.");
            var path = new StringBuilder(64);
            if (!GetVolumeNameForVolumeMountPointW(root, path, path.Capacity) || !DriveOptimization.ValidVolume(path.ToString()))
                throw new IOException("A stable volume identity could not be established.");
            string volume = path.ToString();
            label=ReadLabel(volume); // Bind the displayed label to this verified volume, not a drive letter that can be remapped.
            using var handle = Open(volume.TrimEnd('\\'));
            var filesystem = new StringBuilder(32);
            if (!GetVolumeInformationW(volume, IntPtr.Zero, 0, out uint serial, out _, out uint flags, filesystem, filesystem.Capacity) || filesystem.ToString() != "NTFS")
                throw new IOException("This first version supports accessible NTFS volumes only.");
            if ((flags & 0x80000) != 0) throw new IOException("The filesystem is read-only."); // FILE_READ_ONLY_VOLUME
            var dirty = Control(handle, 0x00090078, [], 4); // FSCTL_IS_VOLUME_DIRTY; query only
            if (dirty.Length < 4) throw new IOException("Filesystem dirty-state information is incomplete.");
            if ((BitConverter.ToUInt32(dirty) & 1) != 0) throw new IOException("Windows marked this volume dirty. Review filesystem health before optimizing.");
            var extents = Control(handle, 0x00560000, [], 4096); // IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS
            var partition = Control(handle, 0x00070048, [], 144); // IOCTL_DISK_GET_PARTITION_INFO_EX
            var layout = DriveStorageLayout.ParseBasic(extents, partition);
            uint disk = layout.Disk;
            using var physical = Open(@"\\.\PhysicalDrive" + disk.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _ = Control(physical, 0x00070024, [], 0); // IOCTL_DISK_IS_WRITABLE; no writes
            var descriptor = Property(physical, 0, 4096);
            if (descriptor.Length < 36 || BitConverter.ToUInt32(descriptor) < 36 || BitConverter.ToUInt32(descriptor, 4) < 36 || descriptor[8] != 0) throw new IOException("Storage descriptors are unavailable or the device is not a direct-access disk.");
            int bus = BitConverter.ToInt32(descriptor, 28);
            bool removable = descriptor[10] != 0;
            bool? seek = BooleanProperty(physical, 7), trim = BooleanProperty(physical, 8);
            var medium = DriveOptimization.Classify(bus, removable, seek, trim);
            if (!GetDiskFreeSpaceExW(volume, out _, out ulong capacity, out ulong free)) throw new IOException("Space information is unavailable.");
            string identity = $"{volume}|{serial:X8}|{disk}|{layout.Offset}|{layout.Length}|{bus}|{seek}|{trim}";
            return new(root, volume, medium, trim == true, true, capacity, free, identity,
                medium == DriveMedium.Unknown ? "Windows could not establish consistent direct HDD/SSD evidence. RAID, USB, virtual and unknown controllers are excluded." : "NTFS basic partition and native storage descriptors verified."){Label=label};
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { return new(root, "", DriveMedium.Unknown, false, false, 0, 0, "", ex.Message){Label=label}; }
    }
    private static string ReadLabel(string root)
    {
        // Cosmetic only: labels never identify an operation target or grant eligibility.
        try {var label=new StringBuilder(261);var filesystem=new StringBuilder(32);return GetVolumeLabelW(root,label,label.Capacity,out _,out _,out _,filesystem,filesystem.Capacity)?FriendlyNames.CleanLabel(label.ToString()):"";}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){return "";}
    }

    internal static async Task<int> RunAsync(DriveCandidate expected, DriveAction action, DriveAnalysis? analysis, Action<string> log)
    {
        // Re-probe immediately before launching. Never pass a mutable drive letter to defrag.exe.
        var current = await Task.Run(() => Probe(expected.Root));
        DriveOptimization.Authorize(expected, current, action, analysis, AcPower(), DateTimeOffset.UtcNow);
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var start = new ProcessStartInfo(Path.Combine(system, "defrag.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = system };
        foreach (string argument in DriveOptimization.Arguments(current, action)) start.ArgumentList.Add(argument);
        log($"Windows {action} on {current.Root} ({current.Medium}); default low priority, one volume only.");
        using var process = new Process { StartInfo = start };
        process.Start();
        // A bounded channel keeps output and dispatcher work bounded even if a tool is very noisy.
        var output = System.Threading.Channels.Channel.CreateBounded<string>(new System.Threading.Channels.BoundedChannelOptions(128)
        { FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait, SingleReader = true });
        async Task Pump(StreamReader reader)
        {
            char[] buffer = new char[512]; int count;
            while ((count = await reader.ReadAsync(buffer)) > 0)
                await output.Writer.WriteAsync(new string(buffer, 0, count).Replace("\0", "").Replace('\r', '\n'));
        }
        async Task Collect()
        {
            int budget = 150_000;
            await foreach (string chunk in output.Reader.ReadAllAsync())
            {
                if (budget <= 0) continue;
                string text = chunk.Length <= budget ? chunk : chunk[..budget]; budget -= text.Length;
                log(text);
                if (budget == 0) log("Further Windows output omitted to keep the report bounded.");
            }
        }
        async Task ReadBoth()
        {
            try { await Task.WhenAll(Pump(process.StandardOutput), Pump(process.StandardError)); }
            finally { output.Writer.TryComplete(); }
        }
        // Do not kill an optimization process on a timer or UI close. Windows completes the current operation.
        await Task.WhenAll(ReadBoth(), Collect(), process.WaitForExitAsync());
        log($"Windows {action} exit code: {process.ExitCode}. Review the report; no performance gain is guaranteed.");
        return process.ExitCode;
    }

    private static bool? AcPower() => GetSystemPowerStatus(out var power) ? power.Ac == 1 ? true : power.Ac == 0 ? false : null : null;
    private static SafeFileHandle Open(string path)
    {
        var handle = CreateFileW(path, 0x80000000, 7, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new System.ComponentModel.Win32Exception(error); }
        return handle;
    }
    private static byte[] Control(SafeFileHandle handle, uint code, byte[] input, int size)
    {
        byte[] output = new byte[size];
        if (!DeviceIoControl(handle, code, input, input.Length, output, output.Length, out int returned, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        if (returned < 0 || returned > output.Length) throw new IOException("Invalid native storage response length.");
        return output[..returned];
    }
    private static byte[] Property(SafeFileHandle handle, int property, int size)
    {
        byte[] query = new byte[12]; BitConverter.GetBytes(property).CopyTo(query, 0);
        return Control(handle, 0x002D1400, query, size); // IOCTL_STORAGE_QUERY_PROPERTY, PropertyStandardQuery
    }
    private static bool? BooleanProperty(SafeFileHandle handle, int property)
    {
        try { return DriveStorageLayout.BooleanProperty(Property(handle, property, 12)); }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct PowerStatus { public byte Ac, Flags, Percent, Reserved; public uint Life, FullLife; }
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)] private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize, [Out] byte[] output, int outputSize, out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern uint GetDriveTypeW(string root);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetVolumeNameForVolumeMountPointW(string root, StringBuilder name, int size);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetVolumeInformationW(string root, IntPtr label, int labelSize, out uint serial, out uint maxComponent, out uint flags, StringBuilder filesystem, int size);
    [DllImport("kernel32.dll",EntryPoint="GetVolumeInformationW",ExactSpelling=true,SetLastError=true,CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetVolumeLabelW(string root,StringBuilder label,int labelSize,out uint serial,out uint maxComponent,out uint flags,StringBuilder filesystem,int size);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetDiskFreeSpaceExW(string path, out ulong available, out ulong capacity, out ulong free);
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemPowerStatus(out PowerStatus power);
}
