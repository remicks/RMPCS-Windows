namespace RMPCS.Core;

public sealed record CleanupPreview(IReadOnlyList<string> Files, long Bytes, int Skipped, DateTime CutoffUtc);
public sealed record CleanupResult(int Deleted, long Bytes, int Skipped);

// Only the application's fixed temp roots may be supplied in production.
// Never follows junctions/symlinks, deletes directories, or clears read-only flags.
public sealed class TempCleaner
{
    private readonly string[] roots;
    private readonly int minimumAgeDays;
    private readonly Func<string, bool> include;
    public TempCleaner(IEnumerable<string> allowedRoots, int minimumAgeDays = 7, Func<string, bool>? include = null)
    {
        if (minimumAgeDays is < 0 or > 365) throw new ArgumentOutOfRangeException(nameof(minimumAgeDays));
        this.minimumAgeDays = minimumAgeDays;
        this.include = include ?? (_ => true);
        roots = allowedRoots.Select(Path.GetFullPath).Select(Path.TrimEndingDirectorySeparator).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (roots.Length == 0 || roots.Any(p => string.Equals(p, Path.GetPathRoot(p), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A filesystem root cannot be a cleanup target.");
    }

    public CleanupPreview Scan(DateTime nowUtc)
    {
        var files = new List<string>();
        long bytes = 0;
        int skipped = 0;
        var cutoff = nowUtc.AddDays(-minimumAgeDays);
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.TryPop(out var directory))
            {
                try
                {
                    // The directory and all of its ancestors are verified once here. Entries inside it only
                    // need their own containment and reparse check, instead of re-walking every ancestor
                    // per file (the preview's dominant cost in large temp folders). Clean() still re-runs the
                    // full ancestor walk and handle-based final-path verification before any deletion.
                    if (!IsSafe(directory, root)) { skipped++; continue; }
                    foreach (var path in Directory.GetFileSystemEntries(directory))
                    {
                        try
                        {
                            if (!IsInside(path, root)) { skipped++; continue; }
                            var attributes = File.GetAttributes(path);
                            if ((attributes & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                            if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                            var info = new FileInfo(path);
                            if (!include(path)) continue;
                            if (info.LastWriteTimeUtc >= cutoff || info.CreationTimeUtc >= cutoff) continue;
                            files.Add(path);
                            bytes += info.Length;
                        }
                        catch (Exception ex) when (IsFileError(ex)) { skipped++; }
                    }
                }
                catch (Exception ex) when (IsFileError(ex)) { skipped++; }
            }
        }
        return new CleanupPreview(files.AsReadOnly(), bytes, skipped, cutoff);
    }

    public CleanupResult Clean(CleanupPreview preview)
    {
        int deleted = 0, skipped = preview.Skipped;
        long bytes = 0;
        foreach (var path in preview.Files)
        {
            try
            {
                var root = roots.FirstOrDefault(r => IsInside(path, r));
                if (root is null || !IsSafe(path, root) || !include(path)) { skipped++; continue; }
                var length = WindowsFile.DeleteEligible(path, root, preview.CutoffUtc);
                if (length is null) { skipped++; continue; }
                deleted++;
                bytes += length.Value;
            }
            catch (Exception ex) when (IsFileError(ex)) { skipped++; }
        }
        return new CleanupResult(deleted, bytes, skipped);
    }

    private static bool IsInside(string path, string root) => Path.GetFullPath(path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static bool IsSafe(string path, string root)
    {
        var full = Path.GetFullPath(path);
        if (!string.Equals(full, root, StringComparison.OrdinalIgnoreCase) && !IsInside(full, root)) return false;
        // Inspect all ancestors too, including a root redirected by a junction.
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }
    private static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}
