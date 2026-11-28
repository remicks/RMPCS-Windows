using RMPCS.Core;

internal static class DriveOptimizationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var now = DateTimeOffset.UtcNow;
        var hdd = new DriveCandidate(@"C:\", @"\\?\Volume{12345678-1234-1234-1234-123456789abc}\", DriveMedium.HardDisk, false, true, 1000, 200, "verified-fixture", "");
        var ssd = hdd with { Medium = DriveMedium.SolidState, TrimSupported = true };
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } check(rejected, name); }
        foreach (int bus in new[] { 3, 11, 17 })
        {
            check(DriveOptimization.Classify(bus, false, false, true) == DriveMedium.SolidState, "direct SSD with TRIM is recognized: bus " + bus);
            check(DriveOptimization.Classify(bus, true, false, true) == DriveMedium.Unknown, "removable SSD never authorized: bus " + bus);
        }
        foreach (int bus in new[] { 0, 1, 2, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16, 18, 19, 99 })
            check(DriveOptimization.Classify(bus, false, true, false) == DriveMedium.Unknown && DriveOptimization.Classify(bus, false, false, true) == DriveMedium.Unknown, "ambiguous bus excluded: " + bus);
        foreach (bool? seek in new bool?[] { null, true, false })
            foreach (bool? trim in new bool?[] { null, true, false })
            {
                var type = DriveOptimization.Classify(11, false, seek, trim);
                check(type == (seek == false && trim == true ? DriveMedium.SolidState : seek == true && trim == false ? DriveMedium.HardDisk : DriveMedium.Unknown), "contradictory or absent media evidence fails closed");
            }
        check(DriveOptimization.Classify(17, false, true, false) == DriveMedium.Unknown, "rotational evidence on NVMe is rejected");
        foreach (string root in new[] { @"C:\folder", "C:", @"\\server\share", "c:\\", "C:\\ & echo x", "C:\\\n", "/c", "" })
            check(!DriveOptimization.ValidRoot(root), "noncanonical root cannot become a command target");
        foreach (string path in new[] { @"C:\", hdd.VolumePath + " /D", hdd.VolumePath + "\n", @"\\?\Volume{00000000-0000-0000-0000-000000000000}\", hdd.VolumePath.Replace("12345678", "x2345678"), "" })
            Reject(() => DriveOptimization.Arguments(hdd with { VolumePath = path }, DriveAction.Analyze), "invalid volume GUID rejected before command construction");
        check(DriveOptimization.Arguments(ssd, DriveAction.ReTrim).SequenceEqual(new[] { ssd.VolumePath, "/L", "/U", "/V" }), "SSD command requests only ReTRIM on the stable volume GUID");
        check(DriveOptimization.Arguments(hdd, DriveAction.Defragment).SequenceEqual(new[] { hdd.VolumePath, "/D", "/U", "/V" }), "HDD command is single-volume defrag at default low priority");
        check(DriveOptimization.Arguments(hdd, DriveAction.Analyze)[1] == "/A", "analysis uses an explicit non-optimization action");
        Reject(() => DriveOptimization.Arguments(ssd, DriveAction.Defragment), "traditional defrag is forbidden on SSDs");
        Reject(() => DriveOptimization.Arguments(hdd, DriveAction.ReTrim), "ReTRIM is forbidden on HDD evidence");
        Reject(() => DriveOptimization.Arguments(hdd, (DriveAction)999), "unknown operations rejected");
        foreach (var drive in new[] { hdd with { Eligible = false }, hdd with { Medium = DriveMedium.Unknown }, hdd with { Free = 1001 }, hdd with { Capacity = 0 }, hdd with { Identity = "" }, hdd with { TrimSupported = true }, ssd with { TrimSupported = false }, hdd with { Medium = (DriveMedium)99 } })
            check(!DriveOptimization.Availability(drive).Optimize, "unsupported, inconsistent or unverified snapshots cannot optimize");
        check(DriveOptimization.Availability(hdd with { Free = 149 }).Analyze && !DriveOptimization.Availability(hdd with { Free = 149 }).Optimize && DriveOptimization.Availability(hdd with { Free = 150 }).Optimize, "HDD 15% free-space guard preserves read-only analysis");
        var analysis = new DriveAnalysis(hdd.Identity, now.AddMinutes(-1));
        byte[] extents = new byte[32], partition = new byte[144];
        BitConverter.GetBytes(1u).CopyTo(extents, 0); BitConverter.GetBytes(3u).CopyTo(extents, 8);
        BitConverter.GetBytes(1048576L).CopyTo(extents, 16); BitConverter.GetBytes(1000000L).CopyTo(extents, 24);
        BitConverter.GetBytes(1u).CopyTo(partition, 0); BitConverter.GetBytes(1048576L).CopyTo(partition, 8);
        BitConverter.GetBytes(1000000L).CopyTo(partition, 16); BitConverter.GetBytes(1u).CopyTo(partition, 24);
        new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7").ToByteArray().CopyTo(partition, 32);
        check(DriveStorageLayout.ParseBasic(extents, partition) == new DriveExtent(3, 1048576, 1000000), "native GPT and extent alignment resolves a single basic partition");
        var mbr = (byte[])partition.Clone(); Array.Clear(mbr, 32, 112); BitConverter.GetBytes(0u).CopyTo(mbr, 0); mbr[32] = 7;
        check(DriveStorageLayout.ParseBasic(extents, mbr).Disk == 3, "basic NTFS MBR partition accepted");
        foreach (int length in new[] { 0, 4, 8, 16, 31 })
        {
            bool bad = false; try { DriveStorageLayout.ParseBasic(extents[..length], partition); } catch (InvalidDataException) { bad = true; }
            check(bad, "truncated native extents rejected without out-of-bounds reads");
        }
        foreach (int length in new[] { 0, 24, 32, 48, 143 })
        {
            bool bad = false; try { DriveStorageLayout.ParseBasic(extents, partition[..length]); } catch (InvalidDataException) { bad = true; }
            check(bad, "truncated native partition rejected without out-of-bounds reads");
        }
        foreach (var change in new Action<byte[], byte[]>[] {
            (e,p) => BitConverter.GetBytes(2u).CopyTo(e, 0), (e,p) => p[29] = 1,
            (e,p) => p[32] = 0, (e,p) => BitConverter.GetBytes(2u).CopyTo(p, 0), (e,p) => p[24] = 0,
            (e,p) => BitConverter.GetBytes(1UL << 60).CopyTo(p, 64), (e,p) => BitConverter.GetBytes(1UL << 62).CopyTo(p, 64),
            (e,p) => BitConverter.GetBytes(999L).CopyTo(e, 16), (e,p) => BitConverter.GetBytes(-1L).CopyTo(e, 24) })
        {
            var e = (byte[])extents.Clone(); var p = (byte[])partition.Clone(); change(e,p);
            bool bad = false; try { DriveStorageLayout.ParseBasic(e,p); } catch (InvalidDataException) { bad = true; }
            check(bad, "multi-extent, service, recovery, dynamic, hidden, read-only and inconsistent native layouts rejected");
        }
        byte[] descriptor = new byte[12]; BitConverter.GetBytes(12u).CopyTo(descriptor,0); BitConverter.GetBytes(12u).CopyTo(descriptor,4); descriptor[8] = 1;
        check(DriveStorageLayout.BooleanProperty(descriptor) == true, "complete native Boolean descriptor decoded");
        descriptor[8] = 2; check(DriveStorageLayout.BooleanProperty(descriptor) is null, "malformed native Boolean remains unknown");
        foreach (int length in new[] { 0, 4, 8 }) check(DriveStorageLayout.BooleanProperty(descriptor[..length]) is null, "truncated native Boolean remains unknown");
        DriveOptimization.Authorize(hdd, hdd, DriveAction.Defragment, analysis, true, now);
        DriveOptimization.Authorize(ssd, ssd, DriveAction.ReTrim, null, true, now);
        DriveOptimization.Authorize(hdd, hdd, DriveAction.Analyze, null, null, now);
        check(true, "valid HDD analysis, SSD ReTRIM and power-independent read-only analysis pass");
        foreach (bool? power in new bool?[] { null, false })
            Reject(() => DriveOptimization.Authorize(ssd, ssd, DriveAction.ReTrim, null, power, now), "battery or unknown power blocks optimization");
        foreach (var changed in new[] { hdd with { Identity = "other" }, hdd with { VolumePath = ssd.VolumePath.Replace("12345678", "22345678") }, hdd with { Root = @"D:\" }, hdd with { Medium = DriveMedium.SolidState, TrimSupported = true }, hdd with { Eligible = false }, hdd with { Free = 1 } })
            Reject(() => DriveOptimization.Authorize(hdd, changed, DriveAction.Defragment, analysis, true, now), "drive identity, media, eligibility and capacity are rechecked");
        foreach (var stale in new DriveAnalysis?[] { null, analysis with { Identity = "other" }, analysis with { Time = now.AddMinutes(-16) }, analysis with { Time = now.AddMinutes(1) } })
            Reject(() => DriveOptimization.Authorize(hdd, hdd, DriveAction.Defragment, stale, true, now), "missing, stale, future or foreign HDD analysis blocks changes");
        check(AppPreferences.Pages.SequenceEqual(PageCatalog.All.Select(p => p.Key)) && new AppPreferences { StartPage = "Drives" }.Normalize().StartPage == "Drives", "drive page order matches desktop/sidebar navigation and saved preferences");
    }
}
