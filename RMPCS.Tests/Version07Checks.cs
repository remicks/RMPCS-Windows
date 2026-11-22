using System.IO.Compression;
using System.Runtime.InteropServices;
using RMPCS.Core;

internal static class Version07Checks
{
    public static async Task RunAsync(Action<bool,string> check, string fixture)
    {
        var result = await MaintenanceRunner.RunAsync(new() { UserTemp = true, RecycleBin = true }, new Counters(), _ => { });
        check(result.Cleanup.Count == 2 && result.Cleanup[0].Bytes == 100 && result.Cleanup[1].Area == CleanupArea.RecycleBin, "runner retains per-category counters without mixing bin estimates");
        string summary = RunSummary.Format(result, TimeSpan.FromSeconds(2), [new("C:\\", 1000)], [new("C:\\", 900)]);
        check(summary.Contains("logical size: 100") && summary.Contains("Recycle Bin decrease (estimate): 2") && summary.Contains("observed change -100"), "summary separates logical size, bin estimates and negative disk deltas");
        check(summary.Contains("not attributed solely") && summary.Contains("does not prove"), "summary avoids claiming all observed gains or repairs are caused/proven by RMPCS");
        var failed = await MaintenanceRunner.RunAsync(new() { UserTemp = true }, new Counters { Fail = true }, _ => { });
        check(failed.Failed == 1 && !failed.Cleanup.Single().Complete && RunSummary.Format(failed, TimeSpan.Zero, [], []).Contains("partial work"), "failed cleanup reports incomplete counters explicitly");
        check(RunSummary.Format(result, TimeSpan.Zero, [new("C:\\", null)], []).Contains("unavailable"), "missing drive observations do not imply zero change");
        var overflow = result with { Cleanup = [new(CleanupArea.UserTemp, 1, long.MaxValue, 0, true), new(CleanupArea.WindowsTemp, 1, 1, 0, true)] };
        check(RunSummary.Format(overflow, TimeSpan.Zero, [], []).Contains("totals unavailable"), "summary counter overflow does not produce misleading totals");
        check(CompletionNotices.From(new(0,0,false,false)) == CompletionNotice.Finished && CompletionNotices.From(new(1,0,false,false)) == CompletionNotice.NeedsReview && CompletionNotices.From(new(0,1,false,false)) == CompletionNotice.NeedsReview, "completion notices distinguish failed or skipped work from a finished queue");
        check(CompletionNotices.From(new(0,0,true,false)) == CompletionNotice.Stopped && CompletionNotices.From(new(0,0,false,true)) == CompletionNotice.RestartPending, "completion notices preserve stop and pending-restart states");
        check(CompletionNotices.Text((CompletionNotice)999) == "" && !new AppPreferences().CompletionNotifications, "unknown notice codes have no text and completion notifications default off");
        check(Enum.GetValues<CompletionNotice>().All(n => CompletionNotices.Text(n).Length is > 0 and < 256), "notification text is a fixed bounded vocabulary");
        string root = Path.Combine(fixture,"archives"); Directory.CreateDirectory(root);
        string LogName(int index) => $"RMPCS-20260101-000000-{index:x32}.txt";
        var created = new List<string>();
        string FileAt(int index, string text) {string path=Path.Combine(root,LogName(index)); File.WriteAllText(path,text);created.Add(path);return path;}
        try
        {
            string first = FileAt(1,"private fixture log"), second = FileAt(2,"read-only fixture log");
            File.SetAttributes(second,FileAttributes.ReadOnly);
            using var zipBytes = new MemoryStream();
            var archive = DiagnosticArchive.Create(zipBytes,root,[Path.GetFileName(first),Path.GetFileName(second),"../escape.txt",Path.GetFileName(first)]);
            check(archive.Included == 2 && archive.Skipped == 1 && File.ReadAllText(first) == "private fixture log", "archive preserves originals, supports read-only logs and deduplicates selections");
            zipBytes.Position=0;
            using(var zip = new ZipArchive(zipBytes,ZipArchiveMode.Read,true))
            {
                check(zip.Entries.Count==3 && zip.Entries.All(e=>!e.FullName.Contains('/') && !e.FullName.Contains('\\')) && zip.GetEntry("ARCHIVE-README.txt") is not null, "archive has safe leaf names and a review manifest");
                using var reader=new StreamReader(zip.GetEntry(Path.GetFileName(first))!.Open());
                check(reader.ReadToEnd()=="private fixture log", "archive contains exact selected log bytes");
            }
            string active=FileAt(3,"active");
            using(var held=new FileStream(active,FileMode.Open,FileAccess.ReadWrite,FileShare.Read))
            using(var output=new MemoryStream())
                check(DiagnosticArchive.Create(output,root,[Path.GetFileName(active)]).Included==0, "archive skips active logs without denying existing writer access");
            string large=FileAt(4,""); using(var file=File.OpenWrite(large))file.SetLength(DiagnosticArchive.MaximumFileBytes+1);
            using(var output=new MemoryStream())check(DiagnosticArchive.Create(output,root,[Path.GetFileName(large)]).Skipped==1,"archive refuses oversized source logs");
            var budgetNames = new List<string>();
            for(int i=201;i<=205;i++)
            {
                string path=FileAt(i,""); using(var file=File.OpenWrite(path))file.SetLength(DiagnosticArchive.MaximumFileBytes);
                budgetNames.Add(Path.GetFileName(path));
            }
            using(var output=new MemoryStream())
            {
                var bounded=DiagnosticArchive.Create(output,root,budgetNames);
                check(bounded.Included==4 && bounded.Skipped==1 && bounded.SourceBytes==DiagnosticArchive.MaximumTotalBytes,"archive enforces its combined source-byte budget across files");
            }
            using(var output=new MemoryStream())check(DiagnosticArchive.Create(output,root,Enumerable.Range(100,31).Select(LogName)).Limited,"archive bounds the number of selected logs");
            check(!DiagnosticArchive.IsLogName("RMPCS-20260101-000000-"+new string('a',32)+".txt\n") && !DiagnosticArchive.IsLogName("C:\\private.txt"), "archive filenames reject trailing control characters and absolute paths");
            string linked=Path.Combine(root,LogName(5));
            if(CreateHardLink(linked,first,IntPtr.Zero))
            {
                created.Add(linked);
                using(var output=new MemoryStream())check(DiagnosticArchive.Create(output,root,[Path.GetFileName(linked)]).Included==0,"archive refuses hard-linked sources");
            }
            else Console.WriteLine("SKIP: hard-link archive fixture unavailable.");
            string destination=Path.Combine(root,"test.zip");created.Add(destination);
            using(var file=new FileStream(destination,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
            {
                DiagnosticArchive.ValidateDestination(file,destination);
                bool rejected=false;try{DiagnosticArchive.ValidateDestination(file,Path.Combine(root,"different.zip"));}catch(IOException){rejected=true;}
                check(rejected && file.Length==0,"archive destination is verified through its held handle before writing");
            }
        }
        finally
        {
            if(!Path.GetFullPath(root).StartsWith(Path.GetFullPath(fixture)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Fixture cleanup escaped its root");
            foreach(string path in created){if(File.Exists(path)){File.SetAttributes(path,FileAttributes.Normal);File.Delete(path);}}
            if(Directory.Exists(root)&&Directory.GetFileSystemEntries(root).Length==0)Directory.Delete(root);
        }
    }
    private sealed class Counters : IMaintenanceOperations
    {
        public bool Fail {get;init;}
        public Task<CleanupResult> CleanAsync(CleanupArea area,int days) => Fail ? throw new IOException("fixture") : Task.FromResult(area==CleanupArea.RecycleBin?new CleanupResult(2,200,0):new CleanupResult(1,100,0));
        public Task<int> RunAsync(SystemCommand command)=>Task.FromResult(0);
        public Task ResetSearchAsync()=>Task.CompletedTask;
    }
    [DllImport("kernel32.dll",EntryPoint="CreateHardLinkW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CreateHardLink(string link,string existing,IntPtr reserved);
}
