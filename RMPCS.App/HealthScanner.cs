using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using RMPCS.Core;

namespace RMPCS.App;

internal static class HealthScanner
{
    public static async Task<string> ScanAsync()
    {
        var text = new StringBuilder($"Rusty's PC Toolkit health snapshot - {DateTimeOffset.Now:O}\n\n");
        try
        {
            if (!GetSystemTimes(out var idle0, out var kernel0, out var user0)) throw new IOException("CPU counters unavailable.");
            await Task.Delay(600);
            if (!GetSystemTimes(out var idle1, out var kernel1, out var user1)) throw new IOException("CPU counters unavailable.");
            ulong total = kernel1 + user1 - kernel0 - user0, idle = idle1 - idle0;
            if (total == 0) throw new IOException("CPU counters did not advance during this sample.");
            double cpu = total > 0 ? Math.Clamp(100d * (1d - (double)idle / total), 0, 100) : 0;
            text.AppendLine($"CPU: {cpu:N1}% over a short sample; {Environment.ProcessorCount} logical processors.");
            text.AppendLine(HealthRules.CpuAdvice(cpu));
        }
        catch (Exception ex) { text.AppendLine("CPU check unavailable: " + ex.Message); }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (GlobalMemoryStatusEx(ref memory))
        {
            text.AppendLine($"\nRAM: {memory.Load}% used; {memory.AvailablePhysical / 1073741824d:N1} GB available of {memory.TotalPhysical / 1073741824d:N1} GB.");
            text.AppendLine(HealthRules.MemoryAdvice(memory.Load));
        }
        else text.AppendLine("RAM check unavailable.");
        text.AppendLine($"\nUptime: {TimeSpan.FromMilliseconds(Environment.TickCount64):d\\d\\ hh\\h\\ mm\\m}");
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            try
            {
                if (!drive.IsReady) continue;
                if (drive.TotalSize <= 0) throw new IOException("Drive capacity unavailable.");
                text.AppendLine($"\n{drive.Name}: {drive.AvailableFreeSpace / 1073741824d:N1} GB free / {drive.TotalSize / 1073741824d:N1} GB ({100d * drive.AvailableFreeSpace / drive.TotalSize:N1}% free).");
                text.AppendLine(HealthRules.StorageAdvice(drive.AvailableFreeSpace, drive.TotalSize));
            }
            catch (Exception ex) { text.AppendLine("Drive check unavailable: " + ex.Message); }
        }
        text.AppendLine("\nRESTART INDICATORS (common indicators; not an exhaustive test)");
        try
        {
            var flags = new List<string>();
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var servicing = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            using var updates = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            using var session = machine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            if (servicing is not null) flags.Add("Windows servicing requests a restart.");
            if (updates is not null) flags.Add("Windows Update requests a restart.");
            if (session?.GetValue("PendingFileRenameOperations") is string[] { Length: > 0 }) flags.Add("Pending file operations detected; a restart may be needed.");
            text.AppendLine(flags.Count > 0 ? string.Join('\n', flags) : "No common restart indicators found.");
        }
        catch (Exception ex) { text.AppendLine("Restart check unavailable: " + ex.Message); }
        text.AppendLine("\nRECENT RECORDED FAILURES (up to 10 per log, last 7 days)");
        await Task.Run(() =>
        {
            foreach (string name in new[] { "System", "Application" })
            {
                try
                {
                    string ids = name == "System" ? "(EventID=41 or EventID=6008 or EventID=1001)" : "(EventID=1000 or EventID=1001)";
                    var query = new EventLogQuery(name, PathType.LogName, $"*[System[{ids} and TimeCreated[timediff(@SystemTime) <= 604800000]]]") { ReverseDirection = true };
                    using var reader = new EventLogReader(query);
                    int count = 0;
                    for (; count < 10; count++)
                    {
                        using var record = reader.ReadEvent(TimeSpan.FromSeconds(2));
                        if (record is null) break;
                        text.AppendLine($"{name}: {record.TimeCreated:g} | {record.ProviderName} | event {record.Id}");
                    }
                    if (count == 0) text.AppendLine($"{name}: no matching records found.");
                }
                catch (Exception ex) { text.AppendLine(name + " log unavailable: " + ex.Message); }
            }
        });
        text.AppendLine("\nRecorded failures are clues, not a diagnosis. This scan makes no settings changes, performs no repairs, and does not assess physical disk health. Export the displayed report if you want to keep it.");
        return text.ToString();
    }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
}
