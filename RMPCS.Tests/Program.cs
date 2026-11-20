using RMPCS.Core;

// Isolated fixture only. Never scans or cleans the machine's real temp roots.
var fixture = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "RMPCS-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
var root = Path.Combine(fixture, "allowed");
var outside = Path.Combine(fixture, "allowed-sibling");
Directory.CreateDirectory(root);
Directory.CreateDirectory(outside);
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); checks++; }
string Make(string directory, string name, int days)
{
    var path = Path.Combine(directory, name);
    File.WriteAllText(path, "fixture data");
    File.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-days));
    File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-days));
    return path;
}
try
{
    var old = Make(root, "old.txt", 10);
    var recent = Make(root, "recent.txt", 1);
    var changed = Make(root, "changed.txt", 10);
    var locked = Make(root, "locked.txt", 10);
    var other = Make(outside, "outside.txt", 10);
    var nested = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
    var child = Make(nested, "child.txt", 10);
    var cleaner = new TempCleaner(new[] { root });
    var scan = cleaner.Scan(DateTime.UtcNow);
    Check(scan.Files.Count == 4 && scan.Bytes == 48, "scan finds only old files including nested files");
    Check(File.Exists(old) && File.Exists(child), "preview does not delete files");
    File.SetLastWriteTimeUtc(changed, DateTime.UtcNow);
    using (var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        var result = cleaner.Clean(scan);
        Check(result.Deleted == 2 && result.Bytes == 24 && result.Skipped == 2, "cleanup reports changed and locked files as skipped");
    }
    Check(File.Exists(recent) && File.Exists(changed) && File.Exists(locked), "recent, changed and locked files survive");
    Check(File.Exists(other), "outside-root file survives");
    Check(Directory.Exists(nested), "directories are preserved");
    var forged = new CleanupPreview(new[] { other }, 12, 0, DateTime.UtcNow.AddDays(-7));
    Check(cleaner.Clean(forged).Deleted == 0 && File.Exists(other), "forged sibling path rejected");
    bool rejected = false;
    try { _ = new TempCleaner(new[] { Path.GetPathRoot(root)! }); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "filesystem root rejected");
    var missing = new TempCleaner(new[] { Path.Combine(fixture, "missing") }).Scan(DateTime.UtcNow);
    Check(missing.Files.Count == 0, "missing temp folder handled");
    // Junction creation does not require developer mode / symlink privileges.
    var junction = Path.Combine(root, "link");
    var command = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    command.Arguments = $"/d /c mklink /J \"{junction}\" \"{outside}\"";
    bool linked;
    using (var p = System.Diagnostics.Process.Start(command)!) { var output = p.StandardOutput.ReadToEnd(); var error = p.StandardError.ReadToEnd(); p.WaitForExit(); linked = p.ExitCode == 0; if (!linked) Console.WriteLine("SKIP: junction fixture is blocked by this environment: " + error.Trim()); }
    if (linked)
    {
        Check(!cleaner.Scan(DateTime.UtcNow).Files.Contains(Path.Combine(junction, "outside.txt")), "junction is not traversed");
        Check(new TempCleaner(new[] { junction }).Scan(DateTime.UtcNow).Files.Count == 0, "junction cleanup root rejected");
        Directory.Delete(junction); // Remove the junction itself, never recurse into its target.
    }
    Console.WriteLine($"{checks} checks passed.");
}
finally
{
    // Delete only known fixture files; no recursive removal or link traversal.
    foreach (var folder in new[] { Path.Combine(root, "nested"), root, outside })
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
            if (Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder);
        }
    if (Directory.Exists(fixture) && Directory.GetFileSystemEntries(fixture).Length == 0) Directory.Delete(fixture);
}
