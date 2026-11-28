using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using RMPCS.Core;

namespace RMPCS.App;

internal sealed class WindowsOperations(Action<string> log) : IMaintenanceOperations
{
    public static TempCleaner Cleaner(CleanupArea area, int days)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (area == CleanupArea.ErrorArchives) return new TempCleaner([
            Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportArchive")], days);
        string root = area switch {
            CleanupArea.UserTemp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            CleanupArea.WindowsTemp => Path.Combine(windows, "Temp"),
            CleanupArea.Prefetch => Path.Combine(windows, "Prefetch"),
            CleanupArea.WindowsLogs => Path.Combine(windows, "Logs"),
            CleanupArea.Thumbnails => Path.Combine(local, "Microsoft", "Windows", "Explorer"),
            CleanupArea.CrashDumps => Path.Combine(local, "CrashDumps"),
            _ => throw new ArgumentOutOfRangeException(nameof(area)) };
        return new TempCleaner(new[] { root }, days, area switch {
            CleanupArea.Prefetch => p => Path.GetExtension(p).Equals(".pf", StringComparison.OrdinalIgnoreCase),
            CleanupArea.Thumbnails => p => Path.GetFileName(p).StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(p).Equals(".db", StringComparison.OrdinalIgnoreCase),
            CleanupArea.CrashDumps => p => Path.GetExtension(p).Equals(".dmp", StringComparison.OrdinalIgnoreCase), _ => null });
    }

    public Task<CleanupResult> CleanAsync(CleanupArea area, int minimumAgeDays) => Task.Run(() =>
    {
        if (area == CleanupArea.RecycleBin) { log("Emptying this user's Recycle Bins; reported space is a before/after estimate."); return CleanupPreviewService.EmptyBin(); }
        log("Scanning " + area);
        var cleaner = Cleaner(area, minimumAgeDays);
        return cleaner.Clean(cleaner.Scan(DateTime.UtcNow));
    });

    public static (string Executable, string[] Arguments) CommandLine(SystemCommand command) => command switch
    {
        SystemCommand.FlushDns => ("ipconfig.exe", ["/flushdns"]),
        SystemCommand.RepairImage => ("DISM.exe", ["/Online", "/Cleanup-Image", "/RestoreHealth"]),
        SystemCommand.ScanSystemFiles => ("sfc.exe", ["/scannow"]),
        SystemCommand.ReleaseDhcp => ("ipconfig.exe", ["/release"]),
        SystemCommand.RenewDhcp => ("ipconfig.exe", ["/renew"]),
        SystemCommand.Restart10 => ("shutdown.exe", ["/r", "/t", "10"]),
        SystemCommand.Restart60 => ("shutdown.exe", ["/r", "/t", "60"]),
        SystemCommand.Restart300 => ("shutdown.exe", ["/r", "/t", "300"]),
        SystemCommand.AbortRestart => ("shutdown.exe", ["/a"]),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    public async Task<int> RunAsync(SystemCommand command)
    {
        var (executable, args) = CommandLine(command);
        log(command + ": " + executable + " " + string.Join(' ', args));
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var start = new ProcessStartInfo(Path.Combine(system, executable))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = system };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        process.Start();
        async Task Pump(StreamReader reader)
        {
            char[] buffer = new char[1024];
            var lines = new OutputLines();
            int count;
            while ((count = await reader.ReadAsync(buffer)) > 0)
                foreach (var line in lines.Push(buffer.AsSpan(0, count))) log(line);
            foreach (var line in lines.Flush()) log(line);
        }
        await Task.WhenAll(Pump(process.StandardOutput), Pump(process.StandardError), process.WaitForExitAsync());
        log($"{command}: exit code {process.ExitCode}." + (process.ExitCode == 3010 ? " Restart requested by Windows." : ""));
        return process.ExitCode;
    }

    public Task ResetSearchAsync()
    {
        // This call resets the database using the documented Windows Search API.
        // No service is disabled and no live database file is manually removed.
        object? managerObject = null;
        ISearchCatalogManager? catalog = null;
        try
        {
            managerObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("7D096C5F-AC08-4F1F-BEB7-5C22C517CE39"), true)!)!;
            ((ISearchManager)managerObject).GetCatalog("SystemIndex", out catalog);
            catalog.Reset();
        }
        finally
        {
            if (catalog is not null) Marshal.FinalReleaseComObject(catalog);
            if (managerObject is not null) Marshal.FinalReleaseComObject(managerObject);
        }
        return Task.CompletedTask;
    }

    // ABI prefix in SearchAPI.h; later interface members are not used here.
    [ComImport, Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISearchManager
    {
        void GetIndexerVersionStr([MarshalAs(UnmanagedType.LPWStr)] out string version);
        void GetIndexerVersion(out uint major, out uint minor);
        void GetParameter([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.Struct)] out object value);
        void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.Struct)] ref object value);
        void GetProxyName([MarshalAs(UnmanagedType.LPWStr)] out string value);
        void GetBypassList([MarshalAs(UnmanagedType.LPWStr)] out string value);
        void SetProxy(uint access, [MarshalAs(UnmanagedType.Bool)] bool local, uint port, [MarshalAs(UnmanagedType.LPWStr)] string proxy, [MarshalAs(UnmanagedType.LPWStr)] string bypass);
        void GetCatalog([MarshalAs(UnmanagedType.LPWStr)] string name, out ISearchCatalogManager catalog);
    }
    [ComImport, Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF50"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISearchCatalogManager
    {
        void GetName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetParameter([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.Struct)] out object value);
        void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.Struct)] ref object value);
        void GetCatalogStatus(out uint status, out uint pausedReason);
        void Reset();
    }
}
