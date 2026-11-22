using System.IO;
using RMPCS.Core;

namespace RMPCS.App;

internal sealed class SessionLog : IDisposable
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RMPCS", "Logs");
    private readonly StreamWriter writer;
    private long written;
    public string? Error { get; private set; }
    public string FilePath { get; }
    public SessionLog(string label)
    {
        Directory.CreateDirectory(Folder);
        CheckPath(Folder);
        FilePath = Path.Combine(Folder, $"RMPCS-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
        writer = new StreamWriter(new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        writer.WriteLine($"RMPCS 0.4.0 | {label} | {DateTimeOffset.Now:O}");
        writer.Flush();
        // Retention uses the same handle-validated deletion as maintenance cleanup.
        var retention = new TempCleaner([Folder], 30, p => Path.GetFileName(p).StartsWith("RMPCS-", StringComparison.Ordinal) && Path.GetExtension(p) == ".txt");
        try { retention.Clean(retention.Scan(DateTime.UtcNow)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    private static void CheckPath(string path)
    {
        for (string? item = path; item is not null; item = Path.GetDirectoryName(item))
            if ((File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0) throw new IOException("Log location contains a folder link; logging refused.");
    }
    public void Write(string line)
    {
        lock (writer)
        {
            if (written > 1_000_000) return;
            try { writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {line}"); writer.Flush(); written += line.Length; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error = ex.Message; written = long.MaxValue; }
        }
    }
    public static string[] List()
    {
        if (!Directory.Exists(Folder)) return [];
        CheckPath(Folder);
        return Directory.GetFiles(Folder, "RMPCS-*.txt").Where(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0).OrderByDescending(File.GetLastWriteTimeUtc).Take(30).ToArray();
    }
    public static string Read(string path)
    {
        CheckPath(path);
        if (!List().Contains(path, StringComparer.OrdinalIgnoreCase)) throw new IOException("Unknown log file.");
        using var reader = File.OpenText(path);
        char[] buffer = new char[1_100_000];
        int count = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, count);
    }
    public void Dispose() { try { writer.Dispose(); } catch (IOException) { } }
}
