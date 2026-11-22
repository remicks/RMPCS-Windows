using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
namespace RMPCS.Core;

public sealed record ArchiveResult(int Included, int Skipped, long SourceBytes, bool Limited);
public static class DiagnosticArchive
{
    public const long MaximumFileBytes = 8 * 1024 * 1024, MaximumTotalBytes = 32 * 1024 * 1024;
    private static readonly Regex Name = new(@"\ARMPCS-[0-9]{8}-[0-9]{6}-[0-9a-f]{32}\.txt\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static bool IsLogName(string name) => name.Length <= 100 && Name.IsMatch(name);
    public static void ValidateDestination(FileStream stream, string path) => WindowsFile.ValidateArchiveDestination(stream, path);
    public static ArchiveResult Create(Stream destination, string logFolder, IEnumerable<string> selectedNames)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(logFolder));
        if (root.StartsWith(@"\\", StringComparison.Ordinal) || root.Equals(Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase)) throw new IOException("A local application log folder is required.");
        for (string? current = root; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked log folders are not archived.");
        var names = selectedNames.Distinct(StringComparer.OrdinalIgnoreCase).Take(31).ToArray();
        int included = 0, skipped = 0; long total = 0;
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, true, Encoding.UTF8);
        var manifest = new StringBuilder("RMPCS diagnostic archive\nOnly selected saved RMPCS session logs are included. Logs may contain private local paths and command output. Review before sharing. No health scan or Windows event-log collection was performed.\nLimits: 30 logs, 8 MiB per log, 32 MiB total source data. Active, inaccessible, linked and oversized files are skipped.\n\n");
        foreach (string name in names.Take(30))
        {
            if (!IsLogName(name)) { skipped++; manifest.AppendLine("Skipped unsupported filename."); continue; }
            byte[] payload;
            try
            {
                using var source = WindowsFile.OpenLogRead(Path.Combine(root, name), root, Math.Min(MaximumFileBytes, MaximumTotalBytes - total));
                using var memory = new MemoryStream(); byte[] block = new byte[16384]; int read;
                while ((read = source.Read(block, 0, block.Length)) != 0)
                { if (memory.Length + read > MaximumFileBytes || total + memory.Length + read > MaximumTotalBytes) throw new IOException("Archive budget exceeded."); memory.Write(block, 0, read); }
                payload = memory.ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { skipped++; manifest.AppendLine(name + " — skipped (unavailable, linked, active or over budget)."); continue; }
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var output = entry.Open()) output.Write(payload);
            total += payload.Length; included++;
            manifest.AppendLine(name + " — " + payload.Length + " source bytes");
        }
        if (names.Length > 30) manifest.AppendLine("Selection limited to the first 30 distinct logs.");
        manifest.AppendLine($"\nIncluded: {included}; skipped: {skipped}; source bytes: {total}.");
        using (var writer = new StreamWriter(zip.CreateEntry("ARCHIVE-README.txt").Open(), new UTF8Encoding(false))) writer.Write(manifest.ToString());
        return new(included, skipped, total, names.Length > 30);
    }
}
