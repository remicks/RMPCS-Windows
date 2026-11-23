using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

// Offline MSI authoring with Windows' own Installer API and cabinet compressor.
// Building this package never installs it or registers a scheduled task.
if (OperatingSystem.IsWindows() && args.Length == 4 && args[0] == "--verify") { MsiVerification.Verify(args[1], args[2], args[3]); return; }
if (!OperatingSystem.IsWindows() || args.Length != 3) throw new ArgumentException("Usage: RMPCS.Packaging <published-app> <TaskCleanup.ps1> <output.msi>");
string payload = Path.GetFullPath(args[0]), cleanup = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var media = Path.Combine(Path.GetDirectoryName(output)!, "msi-media-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(media);
var files = Directory.GetFiles(payload).Where(p => !p.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Append(cleanup).ToArray();
if (!files.Any(p => Path.GetFileName(p) == "RMPCS.exe") || files.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length) throw new InvalidOperationException("Invalid or duplicate payload names.");
if (files.Any(p => p.Contains('"') || p.Contains('\n') || p.Contains('\r'))) throw new InvalidOperationException("Unsupported cabinet source path.");
// One embedded cabinet: the legacy default disk limit can split larger payloads.
var ddf = new StringBuilder(".OPTION EXPLICIT\r\n.Set Cabinet=on\r\n.Set Compress=on\r\n.Set CompressionType=MSZIP\r\n.Set MaxDiskSize=0\r\n.Set MaxCabinetSize=0\r\n.Set CabinetNameTemplate=RMPCS.cab\r\n");
ddf.AppendLine($".Set DiskDirectoryTemplate=\"{media}\"");
ddf.AppendLine($".Set InfFileName=\"{Path.Combine(media, "RMPCS.inf")}\"");
ddf.AppendLine($".Set RptFileName=\"{Path.Combine(media, "RMPCS.rpt")}\"");
for (int i = 0; i < files.Length; i++) ddf.AppendLine($"\"{files[i]}\" F{i:0000}");
string ddfPath = Path.Combine(media, "RMPCS.ddf"); File.WriteAllText(ddfPath, ddf.ToString(), Encoding.ASCII);
var cabStart = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "makecab.exe")) { UseShellExecute = false, CreateNoWindow = true };
cabStart.ArgumentList.Add("/F"); cabStart.ArgumentList.Add(ddfPath);
using (var cab = Process.Start(cabStart)!) { cab.WaitForExit(); if (cab.ExitCode != 0) throw new IOException("Cabinet build failed."); }
if (File.Exists(output)) File.Delete(output); // Only the explicitly supplied output file.
using var db = new MsiDatabase(output);
db.Sql("CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`)");
db.Sql("CREATE TABLE `Directory` (`Directory` CHAR(72) NOT NULL, `Directory_Parent` CHAR(72), `DefaultDir` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Directory`)");
db.Sql("CREATE TABLE `Component` (`Component` CHAR(72) NOT NULL, `ComponentId` CHAR(38), `Directory_` CHAR(72) NOT NULL, `Attributes` SHORT NOT NULL, `Condition` CHAR(255), `KeyPath` CHAR(72) PRIMARY KEY `Component`)");
db.Sql("CREATE TABLE `Feature` (`Feature` CHAR(38) NOT NULL, `Feature_Parent` CHAR(38), `Title` CHAR(64) LOCALIZABLE, `Description` CHAR(255) LOCALIZABLE, `Display` SHORT, `Level` SHORT NOT NULL, `Directory_` CHAR(72), `Attributes` SHORT NOT NULL PRIMARY KEY `Feature`)");
db.Sql("CREATE TABLE `FeatureComponents` (`Feature_` CHAR(38) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Feature_`, `Component_`)");
db.Sql("CREATE TABLE `File` (`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL LOCALIZABLE, `FileSize` LONG NOT NULL, `Version` CHAR(72), `Language` CHAR(20), `Attributes` SHORT, `Sequence` LONG NOT NULL PRIMARY KEY `File`)");
db.Sql("CREATE TABLE `Media` (`DiskId` SHORT NOT NULL, `LastSequence` LONG NOT NULL, `DiskPrompt` CHAR(64) LOCALIZABLE, `Cabinet` CHAR(255), `VolumeLabel` CHAR(32), `Source` CHAR(72) PRIMARY KEY `DiskId`)");
db.Sql("CREATE TABLE `MsiFileHash` (`File_` CHAR(72) NOT NULL, `Options` SHORT NOT NULL, `HashPart1` LONG NOT NULL, `HashPart2` LONG NOT NULL, `HashPart3` LONG NOT NULL, `HashPart4` LONG NOT NULL PRIMARY KEY `File_`)");
db.Sql("CREATE TABLE `Shortcut` (`Shortcut` CHAR(72) NOT NULL, `Directory_` CHAR(72) NOT NULL, `Name` CHAR(128) NOT NULL LOCALIZABLE, `Component_` CHAR(72) NOT NULL, `Target` CHAR(255), `Arguments` CHAR(255), `Description` CHAR(255) LOCALIZABLE, `Hotkey` SHORT, `Icon_` CHAR(72), `IconIndex` SHORT, `ShowCmd` SHORT, `WkDir` CHAR(72) PRIMARY KEY `Shortcut`)");
db.Sql("CREATE TABLE `Icon` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)");
db.Sql("CREATE TABLE `Binary` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)");
db.Binary("Icon", "RMPCS.ico", Path.Combine(payload, "RMPCS.ico"));
db.Binary("Binary", "BrandIcon", Path.Combine(payload, "RMPCS.ico"));
db.Sql("CREATE TABLE `RemoveFile` (`FileKey` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) LOCALIZABLE, `DirProperty` CHAR(72) NOT NULL, `InstallMode` SHORT NOT NULL PRIMARY KEY `FileKey`)");
db.Sql("CREATE TABLE `CustomAction` (`Action` CHAR(72) NOT NULL, `Type` SHORT NOT NULL, `Source` CHAR(72), `Target` CHAR(0) PRIMARY KEY `Action`)");
db.Sql("CREATE TABLE `Registry` (`Registry` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL LOCALIZABLE, `Name` CHAR(255) LOCALIZABLE, `Value` CHAR(0) LOCALIZABLE, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Registry`)");
db.Sql("CREATE TABLE `LaunchCondition` (`Condition` CHAR(255) NOT NULL, `Description` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Condition`)");
db.Sql("CREATE TABLE `AppSearch` (`Property` CHAR(72) NOT NULL, `Signature_` CHAR(72) NOT NULL PRIMARY KEY `Property`, `Signature_`)");
db.Sql("CREATE TABLE `RegLocator` (`Signature_` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL, `Name` CHAR(255), `Type` SHORT PRIMARY KEY `Signature_`)");
db.Sql("CREATE TABLE `DrLocator` (`Signature_` CHAR(72) NOT NULL, `Parent` CHAR(72), `Path` CHAR(255), `Depth` SHORT PRIMARY KEY `Signature_`, `Parent`, `Path`)");
db.Sql("CREATE TABLE `Signature` (`Signature` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL, `MinVersion` CHAR(20), `MaxVersion` CHAR(20), `MinSize` LONG, `MaxSize` LONG, `MinDate` LONG, `MaxDate` LONG, `Languages` CHAR(255) PRIMARY KEY `Signature`)");
db.Sql("CREATE TABLE `Upgrade` (`UpgradeCode` CHAR(38) NOT NULL, `VersionMin` CHAR(20), `VersionMax` CHAR(20), `Language` CHAR(255), `Attributes` SHORT NOT NULL, `Remove` CHAR(255), `ActionProperty` CHAR(72) NOT NULL PRIMARY KEY `UpgradeCode`, `VersionMin`, `VersionMax`, `Language`, `Attributes`)");
foreach (var sequence in new[] { "InstallExecuteSequence", "InstallUISequence" }) db.Sql($"CREATE TABLE `{sequence}` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)");

const string upgrade = "{C19B49F3-55BC-4427-AE35-3E1E27B732A5}";
string product = Guid.NewGuid().ToString("B").ToUpperInvariant();
foreach (var property in new Dictionary<string,string> {
    ["ProductCode"] = product, ["UpgradeCode"] = upgrade, ["ProductName"] = "RMPCS - Rusty\u0027s PC Toolkit", ["ProductVersion"] = "0.9.0", ["ProductLanguage"] = "1033",
    ["Manufacturer"] = "Degenerates Inc.", ["ALLUSERS"] = "1", ["ARPNOMODIFY"] = "1", ["ARPPRODUCTICON"] = "RMPCS.ico", ["ARPCOMMENTS"] = "Rusty's PC maintenance toolkit. Automation is opt-in.",
    ["SecureCustomProperties"] = "OLDPRODUCTS;NEWERFOUND;DOTNETDESKTOP10;CREATE_DESKTOP;CREATE_STARTMENU;ALWAYS_ADMIN", ["MSIRESTARTMANAGERCONTROL"] = "Disable", ["MSIDISABLERMRESTART"] = "1", ["INSTALLLEVEL"] = "1", ["RUN_AFTER"] = "1" }) db.Row("Property", property.Key, property.Value);
db.Row("Directory", "TARGETDIR", null, "SourceDir");
db.Row("Directory", "ProgramFiles64Folder", "TARGETDIR", ".");
db.Row("Directory", "VENDORFOLDER", "ProgramFiles64Folder", "DEGENE~1|Degenerates Inc");
db.Row("Directory", "INSTALLFOLDER", "VENDORFOLDER", "RMPCS");
db.Row("Directory", "ProgramMenuFolder", "TARGETDIR", ".");
db.Row("Directory", "DesktopFolder", "TARGETDIR", ".");
db.Row("Directory", "MENUFOLDER", "ProgramMenuFolder", "RMPCS");
db.Row("Directory", "SystemFolder", "TARGETDIR", ".");
db.Row("Directory", "System64Folder", "TARGETDIR", ".");
db.Row("Feature", "Main", null, "RMPCS", "PC maintenance toolkit", 1, 1, "INSTALLFOLDER", 0);
db.Row("Media", 1, files.Length, null, "#RMPCS.cab", null, null);
int exeIndex = Array.FindIndex(files, p => Path.GetFileName(p) == "RMPCS.exe");
for (int i = 0; i < files.Length; i++)
{
    string name = Path.GetFileName(files[i]), id = $"F{i:0000}", component = $"C{i:0000}";
    byte[] guidBytes = SHA256.HashData(Encoding.UTF8.GetBytes("RMPCS.Component.v1/" + name.ToLowerInvariant()))[..16];
    db.Row("Component", component, new Guid(guidBytes).ToString("B").ToUpperInvariant(), "INSTALLFOLDER", 256, null, id);
    db.Row("FeatureComponents", "Main", component);
    var version = FileVersionInfo.GetVersionInfo(files[i]);
    string? fileVersion = version.FileVersion is not null ? $"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}.{version.FilePrivatePart}" : null;
    db.Row("File", id, component, $"R{i:0000}.BIN|{name}", checked((int)new FileInfo(files[i]).Length), fileVersion, null, 512, i + 1);
    if (fileVersion is null)
    {
        var hash = MsiNative.Hash(files[i]);
        db.Row("MsiFileHash", id, 0, hash[0], hash[1], hash[2], hash[3]);
    }
}
string exeComponent = $"C{exeIndex:0000}";
const string optionKey = @"SOFTWARE\DegeneratesInc\RMPCS\InstallOptions";
int optionIndex = 0;
foreach (var (property, valueName) in new[] { ("CREATE_DESKTOP", "Desktop"), ("CREATE_STARTMENU", "StartMenu"), ("ALWAYS_ADMIN", "AlwaysAdmin") })
{
    string previous = "PREVIOUS_" + property;
    db.Row("Registry", "Option" + valueName, 2, optionKey, valueName, "[" + property + "]", exeComponent);
    db.Row("AppSearch", previous, "Previous" + valueName);
    db.Row("RegLocator", "Previous" + valueName, 2, optionKey, valueName, 18);
    string restore = "Restore" + valueName, fallback = "Default" + valueName, normalize = "Normalize" + valueName;
    db.Row("CustomAction", restore, 51, property, "[" + previous + "]");
    db.Row("CustomAction", fallback, 51, property, "1");
    db.Row("CustomAction", normalize, 51, property, "0");
    foreach (var sequence in new[] { "InstallUISequence", "InstallExecuteSequence" })
    {
        string gate = sequence == "InstallExecuteSequence" ? "UILevel < 5 AND " : "";
        db.Row(sequence, restore, gate + "NOT " + property + " AND " + previous, 55 + optionIndex * 2);
        db.Row(sequence, fallback, gate + "NOT " + property + " AND NOT " + previous, 56 + optionIndex * 2);
    }
    db.Row("InstallUISequence", normalize, "NOT " + property, 1150 + optionIndex);
    optionIndex++;
}
foreach (var (id, folder, property) in new[] { ("StartMenu", "MENUFOLDER", "CREATE_STARTMENU"), ("Desktop", "DesktopFolder", "CREATE_DESKTOP") })
{
    string component = "Shortcut" + id, marker = "Marker" + id;
    var componentGuid = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("RMPCS.Shortcut.v2/" + id))[..16]);
    db.Row("Component", component, componentGuid.ToString("B").ToUpperInvariant(), "INSTALLFOLDER", 324, property + " = \"1\"", marker);
    db.Row("Registry", marker, 2, optionKey + @"\ShortcutMarkers", id, "1", component);
    db.Row("FeatureComponents", "Main", component);
    db.Row("Shortcut", id, folder, "RMPCS|Rusty\u0027s PC Toolkit", component, $"[#F{exeIndex:0000}]", null, "Rusty's PC maintenance toolkit", null, "RMPCS.ico", 0, 1, "INSTALLFOLDER");
}
db.Row("RemoveFile", "MenuDirectory", "ShortcutStartMenu", null, "MENUFOLDER", 2);
db.Row("RemoveFile", "InstallDirectory", exeComponent, null, "INSTALLFOLDER", 2);
db.Row("RemoveFile", "VendorDirectory", exeComponent, null, "VENDORFOLDER", 2);
// Search the registered x64 runtime root, including custom dotnet installations.
db.Row("AppSearch", "DOTNETDESKTOP10", "Desktop10File");
db.Row("RegLocator", "DotnetRoot", 2, @"SOFTWARE\dotnet\Setup\InstalledVersions\x64", "InstallLocation", 0);
db.Row("DrLocator", "Desktop10File", "DotnetRoot", @"shared\Microsoft.WindowsDesktop.App", 1);
db.Row("Signature", "Desktop10File", "PresentationFramework.dll", "10.0.0.0", "10.65535.65535.65535", null, null, null, null, null);
db.Row("LaunchCondition", "Installed OR DOTNETDESKTOP10", "Install the Microsoft .NET 10 Desktop Runtime (Windows x64), then run this installer again. Download: https://dotnet.microsoft.com/download/dotnet/10.0");
db.Row("LaunchCondition", "VersionNT64", "RMPCS requires 64-bit Windows.");
db.Row("Upgrade", upgrade, "0.0.0", "0.9.0", "1033", 257, null, "OLDPRODUCTS");
db.Row("Upgrade", upgrade, "0.9.0", null, "1033", 258, null, "NEWERFOUND");
db.Row("LaunchCondition", "Installed OR NOT NEWERFOUND", "The same or a newer RMPCS version is installed. Uninstall it first or use its Repair option.");
string cleanupId = $"F{files.Length - 1:0000}";
db.Row("CustomAction", "CleanupTasks", 3106, "System64Folder", $"\"[System64Folder]WindowsPowerShell\\v1.0\\powershell.exe\" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"[#{cleanupId}]\" -InstallRoot \"[INSTALLFOLDER].\"");
foreach (var (action, number) in new (string,int)[] { ("FindRelatedProducts",25), ("AppSearch",50), ("LaunchConditions",100), ("ValidateProductID",700), ("CostInitialize",800), ("FileCost",900), ("CostFinalize",1000), ("MigrateFeatureStates",1200), ("InstallValidate",1400), ("InstallInitialize",1500), ("RemoveExistingProducts",1510), ("ProcessComponents",1600), ("UnpublishFeatures",1800), ("RemoveRegistryValues",2600), ("RemoveShortcuts",3200), ("RemoveFiles",3500), ("RemoveFolders",3600), ("CreateFolders",3700), ("InstallFiles",4000), ("WriteRegistryValues",5000), ("CreateShortcuts",4500), ("RegisterUser",6000), ("RegisterProduct",6100), ("PublishFeatures",6300), ("PublishProduct",6400), ("InstallFinalize",6600) }) db.Row("InstallExecuteSequence", action, null, number);
db.Row("InstallExecuteSequence", "CleanupTasks", "REMOVE~=\"ALL\" AND NOT UPGRADINGPRODUCTCODE", 3100);

db.Sql("CREATE TABLE `Dialog` (`Dialog` CHAR(72) NOT NULL, `HCentering` SHORT NOT NULL, `VCentering` SHORT NOT NULL, `Width` SHORT NOT NULL, `Height` SHORT NOT NULL, `Attributes` LONG, `Title` CHAR(128) LOCALIZABLE, `Control_First` CHAR(50), `Control_Default` CHAR(50), `Control_Cancel` CHAR(50) PRIMARY KEY `Dialog`)");
db.Sql("CREATE TABLE `Control` (`Dialog_` CHAR(72) NOT NULL, `Control` CHAR(50) NOT NULL, `Type` CHAR(20) NOT NULL, `X` SHORT NOT NULL, `Y` SHORT NOT NULL, `Width` SHORT NOT NULL, `Height` SHORT NOT NULL, `Attributes` LONG, `Property` CHAR(72), `Text` CHAR(0) LOCALIZABLE, `Control_Next` CHAR(50), `Help` CHAR(50) LOCALIZABLE PRIMARY KEY `Dialog_`, `Control`)");
db.Sql("CREATE TABLE `ControlEvent` (`Dialog_` CHAR(72) NOT NULL, `Control_` CHAR(50) NOT NULL, `Event` CHAR(50) NOT NULL, `Argument` CHAR(255), `Condition` CHAR(255), `Ordering` SHORT PRIMARY KEY `Dialog_`, `Control_`, `Event`, `Argument`, `Condition`)");
db.Sql("CREATE TABLE `TextStyle` (`TextStyle` CHAR(72) NOT NULL, `FaceName` CHAR(32) NOT NULL, `Size` SHORT NOT NULL, `Color` LONG, `StyleBits` SHORT PRIMARY KEY `TextStyle`)");
db.Sql("CREATE TABLE `CheckBox` (`Property` CHAR(72) NOT NULL, `Value` CHAR(64) LOCALIZABLE PRIMARY KEY `Property`)");
foreach (string property in new[] { "CREATE_DESKTOP", "CREATE_STARTMENU", "ALWAYS_ADMIN", "RUN_AFTER" }) db.Row("CheckBox", property, "1");
db.Row("TextStyle", "Normal", "Segoe UI", 9, 0, 0); db.Row("Property", "DefaultUIFont", "Normal");
db.Row("TextStyle", "Heading", "Segoe UI", 18, 5649863, 1);
db.Row("TextStyle", "Muted", "Segoe UI", 9, 7366232, 0);
void Dialog(string name, string title, string body, string button, string action, string? property = null)
{
    db.Row("Dialog", name, 50, 50, 420, 285, 3, title, "Continue", "Continue", "Cancel");
    db.Row("Control", name, "Brand", "Icon", 20, 18, 36, 36, 3, null, "BrandIcon", null, null);
    db.Row("Control", name, "Title", "Text", 70, 20, 330, 30, 3, null, "{\\Heading}" + title, null, null);
    db.Row("Control", name, "Subtitle", "Text", 70, 52, 330, 20, 3, null, "{\\Muted}DEGENERATES INC.  /  PC TOOLKIT", null, null);
    db.Row("Control", name, "Divider", "Line", 20, 80, 380, 1, 3, null, null, null, null);
    db.Row("Control", name, "Body", "Text", 24, 94, 372, 130, 3, null, body, null, null);
    db.Row("Control", name, "Footer", "Text", 24, 248, 190, 22, 3, null, "{\\Muted}Startup and schedules are opt-in.", null, null);
    db.Row("Control", name, "Continue", "PushButton", 236, 244, 76, 24, 3, null, button, "Cancel", null);
    db.Row("Control", name, "Cancel", "PushButton", 322, 244, 76, 24, 3, null, "Cancel", "Continue", null);
    if (property is not null) db.Row("ControlEvent", name, "Continue", property, "ALL", "1", 1);
    db.Row("ControlEvent", name, "Continue", "EndDialog", action, "1", 2);
    db.Row("ControlEvent", name, "Cancel", "EndDialog", "Exit", "1", 1);
}
Dialog("WelcomeDlg", "RMPCS 0.9.0 Setup", "Install Rusty's PC maintenance toolkit to:\n[ProgramFiles64Folder]Degenerates Inc\\RMPCS\n\nRequires the .NET 10 Desktop Runtime.\n\nCreates a Start Menu shortcut and an uninstall entry. Startup and schedules remain OFF until you enable them in the app.\n\nClose RMPCS before installing or updating.", "Install", "Return");
db.Sql("UPDATE `Control` SET `Text`='Choose how RMPCS fits into your desktop.\nWindows will still ask for permission when administrator access is requested.\n\nClose RMPCS using Exit before continuing.' WHERE `Dialog_`='WelcomeDlg' AND `Control`='Body'");
db.Sql("UPDATE `Control` SET `Height`=62 WHERE `Dialog_`='WelcomeDlg' AND `Control`='Body'");
db.Sql("UPDATE `Control` SET `Control_Next`='DesktopChoice' WHERE `Dialog_`='WelcomeDlg' AND `Control`='Cancel'");
db.Row("Control", "WelcomeDlg", "DesktopChoice", "CheckBox", 24, 159, 372, 18, 3, "CREATE_DESKTOP", "Create a desktop shortcut", "MenuChoice", null);
db.Row("Control", "WelcomeDlg", "MenuChoice", "CheckBox", 24, 183, 372, 18, 3, "CREATE_STARTMENU", "Create a Start Menu entry", "AdminChoice", null);
db.Row("Control", "WelcomeDlg", "AdminChoice", "CheckBox", 24, 207, 372, 18, 3, "ALWAYS_ADMIN", "Always request administrator access when opening RMPCS", "Continue", null);
Dialog("RemoveDlg", "Remove RMPCS", "Remove the installed application and its startup/maintenance tasks.\n\nClose RMPCS first and allow active repairs to finish.\n\nSaved logs in your user profile are preserved.\n\nUse Windows Installed apps to repair the installation if needed.", "Remove", "Return", "[REMOVE]");
foreach (var (action, number) in new (string,int)[] { ("FindRelatedProducts",25), ("AppSearch",50), ("LaunchConditions",100), ("CostInitialize",800), ("FileCost",900), ("CostFinalize",1000), ("ExecuteAction",1300) }) db.Row("InstallUISequence", action, null, number);
db.Row("InstallUISequence", "WelcomeDlg", "NOT Installed", 750);
foreach (string name in new[] { "Desktop", "StartMenu", "AlwaysAdmin" }) db.Sql($"UPDATE `InstallUISequence` SET `Sequence`={760 + Array.IndexOf(new[] { "Desktop", "StartMenu", "AlwaysAdmin" }, name)} WHERE `Action`='Normalize{name}'");
db.Row("InstallUISequence", "RemoveDlg", "Installed AND NOT REINSTALL", 1101);
db.Row("Dialog", "FinishDlg", 50, 50, 420, 285, 3, "RMPCS is ready", "LaunchChoice", "Finish", "Close");
db.Row("Control", "FinishDlg", "Brand", "Icon", 20, 24, 36, 36, 3, null, "BrandIcon", null, null);
db.Row("Control", "FinishDlg", "Heading", "Text", 70, 26, 325, 32, 3, null, "{\\Heading}You're all set", null, null);
db.Row("Control", "FinishDlg", "Body", "Text", 24, 96, 372, 70, 3, null, "RMPCS was installed successfully.\nStartup to tray and maintenance schedules stay opt-in.\nNo cleanup runs when you open the app.", null, null);
db.Row("Control", "FinishDlg", "LaunchChoice", "CheckBox", 24, 180, 372, 22, 3, "RUN_AFTER", "Run RMPCS after installation", "Finish", null);
db.Row("Control", "FinishDlg", "Finish", "PushButton", 308, 244, 90, 24, 3, null, "Finish", "Close", null);
db.Row("Control", "FinishDlg", "Close", "PushButton", 212, 244, 84, 24, 3, null, "Close", "LaunchChoice", null);
db.Row("ControlEvent", "FinishDlg", "Close", "EndDialog", "Return", "1", 1);
db.Row("CustomAction", "LaunchInstalledApp", 210, $"F{exeIndex:0000}", null);
db.Row("ControlEvent", "FinishDlg", "Finish", "EndDialog", "Return", "1", 1);
db.Row("ControlEvent", "FinishDlg", "Finish", "DoAction", "LaunchInstalledApp", "RUN_AFTER = \"1\"", 2);
db.Row("InstallUISequence", "FinishDlg", "NOT Installed AND NOT REMOVE", -1);
db.Stream("RMPCS.cab", Path.Combine(media, "RMPCS.cab"));
db.Summary(2, "RMPCS Installation"); db.Summary(3, "Rusty's PC maintenance toolkit"); db.Summary(4, "Degenerates Inc.");
db.Summary(7, "x64;1033"); db.Summary(9, Guid.NewGuid().ToString("B").ToUpperInvariant()); db.Summary(14, 500); db.Summary(15, 2); db.Summary(18, "RMPCS offline MSI builder");
db.Commit();
Console.WriteLine($"Created {output} ({new FileInfo(output).Length:N0} bytes). ProductCode {product}");

sealed class MsiDatabase : IDisposable
{
    private readonly uint handle;
    private readonly Dictionary<string, string[]> columns = [];
    public MsiDatabase(string path, bool readOnly = false) { MsiNative.Check(MsiNative.MsiOpenDatabaseW(path, readOnly ? IntPtr.Zero : new IntPtr(3), out handle), "Open database"); }
    public List<string[]> Query(string query)
    {
        MsiNative.Check(MsiNative.MsiDatabaseOpenViewW(handle, query, out uint view), query);
        var rows = new List<string[]>();
        try
        {
            MsiNative.Check(MsiNative.MsiViewExecute(view, 0), "Read view");
            while (true)
            {
                uint code = MsiNative.MsiViewFetch(view, out uint record);
                if (code == 259) break;
                MsiNative.Check(code, "Fetch record");
                try { rows.Add(Enumerable.Range(1, (int)MsiNative.MsiRecordGetFieldCount(record)).Select(i => MsiNative.String(record, (uint)i)).ToArray()); }
                finally { MsiNative.MsiCloseHandle(record); }
            }
        }
        finally { MsiNative.MsiCloseHandle(view); }
        return rows;
    }
    public void ExtractCabinet(string destination)
    {
        MsiNative.Check(MsiNative.MsiDatabaseOpenViewW(handle, "SELECT `Data` FROM `_Streams` WHERE `Name`='RMPCS.cab'", out uint view), "Cabinet read view");
        try
        {
            MsiNative.Check(MsiNative.MsiViewExecute(view, 0), "Cabinet execute view");
            MsiNative.Check(MsiNative.MsiViewFetch(view, out uint record), "Cabinet record");
            try
            {
                using var file = File.Create(destination);
                byte[] buffer = new byte[65536];
                while (true) { uint size = (uint)buffer.Length; MsiNative.Check(MsiNative.MsiRecordReadStream(record, 1, buffer, ref size), "Cabinet stream"); if (size == 0) break; file.Write(buffer, 0, (int)size); }
            }
            finally { MsiNative.MsiCloseHandle(record); }
        }
        finally { MsiNative.MsiCloseHandle(view); }
    }
    public void Sql(string sql) { MsiNative.Check(MsiNative.MsiDatabaseOpenViewW(handle, sql, out uint view), sql); try { MsiNative.Check(MsiNative.MsiViewExecute(view, 0), sql); } finally { MsiNative.MsiCloseHandle(view); } }
    public void Row(string table, params object?[] values)
    {
        uint record = MsiNative.MsiCreateRecord((uint)values.Length);
        try
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] is null) continue;
                uint code = values[i] is int integer ? MsiNative.MsiRecordSetInteger(record, (uint)i + 1, integer) : MsiNative.MsiRecordSetStringW(record, (uint)i + 1, values[i]!.ToString()!);
                MsiNative.Check(code, "Set record");
            }
            if (!columns.TryGetValue(table, out var names))
            {
                MsiNative.Check(MsiNative.MsiDatabaseOpenViewW(handle, $"SELECT * FROM `{table}`", out uint schemaView), "Column view");
                try
                {
                    MsiNative.Check(MsiNative.MsiViewGetColumnInfo(schemaView, 0, out uint info), "Column info");
                    try { names = Enumerable.Range(1, checked((int)MsiNative.MsiRecordGetFieldCount(info))).Select(i => MsiNative.String(info, (uint)i)).ToArray(); }
                    finally { MsiNative.MsiCloseHandle(info); }
                }
                finally { MsiNative.MsiCloseHandle(schemaView); }
                columns[table] = names;
            }
            if (names.Length != values.Length) throw new InvalidOperationException("Column count mismatch: " + table);
            string query = $"INSERT INTO `{table}` ({string.Join(",", names.Select(n => "`" + n + "`"))}) VALUES ({string.Join(",", values.Select(_ => "?"))})";
            MsiNative.Check(MsiNative.MsiDatabaseOpenViewW(handle, query, out uint view), query);
            try { MsiNative.Check(MsiNative.MsiViewExecute(view, record), "Insert " + table + ": " + string.Join(" / ", values)); }
            finally { MsiNative.MsiCloseHandle(view); }
        }
        finally { MsiNative.MsiCloseHandle(record); }
    }
    public void Stream(string name, string path)
    {
        uint record = MsiNative.MsiCreateRecord(2);
        try
        {
            MsiNative.Check(MsiNative.MsiRecordSetStringW(record, 1, name), "Stream name");
            MsiNative.Check(MsiNative.MsiRecordSetStreamW(record, 2, path), "Stream payload");
            MsiNative.Check(MsiNative.MsiDatabaseOpenViewW(handle, "INSERT INTO `_Streams` (`Name`, `Data`) VALUES (?, ?)", out uint view), "Stream view");
            try { MsiNative.Check(MsiNative.MsiViewExecute(view, record), "Insert stream"); } finally { MsiNative.MsiCloseHandle(view); }
        }
        finally { MsiNative.MsiCloseHandle(record); }
    }
    public void Binary(string table, string name, string path)
    {
        uint record = MsiNative.MsiCreateRecord(2);
        try
        {
            MsiNative.Check(MsiNative.MsiRecordSetStringW(record, 1, name), "Binary name");
            MsiNative.Check(MsiNative.MsiRecordSetStreamW(record, 2, path), "Binary data");
            MsiNative.Check(MsiNative.MsiDatabaseOpenViewW(handle, $"INSERT INTO `{table}` (`Name`, `Data`) VALUES (?, ?)", out uint view), "Binary view");
            try { MsiNative.Check(MsiNative.MsiViewExecute(view, record), "Insert binary"); } finally { MsiNative.MsiCloseHandle(view); }
        }
        finally { MsiNative.MsiCloseHandle(record); }
    }
    public void Summary(uint property, object value)
    {
        MsiNative.Check(MsiNative.MsiGetSummaryInformationW(handle, null, 20, out uint info), "Summary open");
        try { MsiNative.Check(MsiNative.MsiSummaryInfoSetPropertyW(info, property, value is int ? 3u : 30u, value is int n ? n : 0, IntPtr.Zero, value as string), "Set summary"); MsiNative.Check(MsiNative.MsiSummaryInfoPersist(info), "Save summary"); }
        finally { MsiNative.MsiCloseHandle(info); }
    }
    public void Commit() => MsiNative.Check(MsiNative.MsiDatabaseCommit(handle), "Commit MSI");
    public void Dispose() => MsiNative.MsiCloseHandle(handle);
}
static class MsiNative
{
    public static void Check(uint code, string operation) { if (code != 0) throw new IOException($"{operation}: Windows Installer error {code}"); }
    [StructLayout(LayoutKind.Sequential)] private struct FileHash { public uint Size; public int A, B, C, D; }
    public static int[] Hash(string path) { var hash = new FileHash { Size = 20 }; Check(MsiGetFileHashW(path, 0, ref hash), "File hash"); return [hash.A, hash.B, hash.C, hash.D]; }
    public static string String(uint record, uint field)
    {
        uint size = 0; uint code = MsiRecordGetStringW(record, field, null, ref size);
        if (code is not (0 or 234)) Check(code, "Read string length");
        var value = new StringBuilder(checked((int)size + 1)); size++;
        Check(MsiRecordGetStringW(record, field, value, ref size), "Read string"); return value.ToString();
    }
    [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] public static extern uint MsiOpenDatabaseW(string path, IntPtr persist, out uint database);
    [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] public static extern uint MsiDatabaseOpenViewW(uint db, string query, out uint view);
    [DllImport("msi.dll")] public static extern uint MsiViewExecute(uint view, uint record);
    [DllImport("msi.dll")] public static extern uint MsiViewFetch(uint view, out uint record);
    [DllImport("msi.dll")] public static extern uint MsiRecordReadStream(uint record, uint field, byte[] buffer, ref uint size);
    [DllImport("msi.dll")] public static extern uint MsiViewGetColumnInfo(uint view, uint kind, out uint record);
    [DllImport("msi.dll")] public static extern uint MsiRecordGetFieldCount(uint record);
    [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] public static extern uint MsiRecordGetStringW(uint record, uint field, StringBuilder? value, ref uint size);
    [DllImport("msi.dll")] public static extern uint MsiCloseHandle(uint handle);
    [DllImport("msi.dll")] public static extern uint MsiCreateRecord(uint fields);
    [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] public static extern uint MsiRecordSetStringW(uint record, uint field, string value);
    [DllImport("msi.dll")] public static extern uint MsiRecordSetInteger(uint record, uint field, int value);
    [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] public static extern uint MsiRecordSetStreamW(uint record, uint field, string path);
    [DllImport("msi.dll")] public static extern uint MsiDatabaseCommit(uint db);
    [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] public static extern uint MsiGetSummaryInformationW(uint db, string? path, uint updateCount, out uint info);
    [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] public static extern uint MsiSummaryInfoSetPropertyW(uint info, uint property, uint type, int number, IntPtr time, string? value);
    [DllImport("msi.dll")] public static extern uint MsiSummaryInfoPersist(uint info);
    [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] private static extern uint MsiGetFileHashW(string path, uint options, ref FileHash hash);
}

static class MsiVerification
{
    public static void Verify(string package, string app, string cleanup)
    {
        using var db = new MsiDatabase(Path.GetFullPath(package), true);
        void Check(bool valid, string description) { if (!valid) throw new InvalidDataException(description); Console.WriteLine("PASS: " + description); }
        var properties = db.Query("SELECT `Property`,`Value` FROM `Property`").ToDictionary(r => r[0], r => r[1]);
        Check(properties["ProductVersion"] == "0.9.0" && properties["ALLUSERS"] == "1", "MSI version and per-machine installation metadata");
        var components = db.Query("SELECT `Component`,`ComponentId`,`Attributes`,`KeyPath` FROM `Component`");
        Check(components.All(c => (int.Parse(c[2]) & 256) != 0 && Guid.TryParse(c[1], out _)), "all installed components are x64 and have valid identities");
        var fileRows = db.Query("SELECT `File`,`Component_`,`FileName`,`FileSize` FROM `File`");
        Check(fileRows.Count + 2 == components.Count && fileRows.All(f => components.Any(c => c[0] == f[1] && c[3] == f[0])), "files and optional shortcuts have separate keyed components");
        var sequence = db.Query("SELECT `Action`,`Condition`,`Sequence` FROM `InstallExecuteSequence`");
        Check(sequence.Single(r => r[0] == "CleanupTasks")[1] == "REMOVE~=\"ALL\" AND NOT UPGRADINGPRODUCTCODE", "uninstall removes automation without removing it during upgrades");
        Check(int.Parse(sequence.Single(r => r[0] == "CleanupTasks")[2]) < int.Parse(sequence.Single(r => r[0] == "RemoveFiles")[2]), "automation removal precedes file removal");
        var locator = db.Query("SELECT `Type` FROM `RegLocator`");
        Check(locator.Count(r => r[0] == "0") == 1 && locator.Count(r => r[0] == "18") == 3, "runtime and saved install choices use their correct registry views");
        Check(db.Query("SELECT `MinVersion` FROM `Signature`").Single()[0] == "10.0.0.0", "installer checks the .NET 10 Desktop Runtime version");
        var shortcuts = db.Query("SELECT `Shortcut`,`Icon_`,`IconIndex` FROM `Shortcut`");
        Check(db.Query("SELECT `Action` FROM `InstallUISequence`").Any(r => r[0] == "WelcomeDlg") && shortcuts.Count == 2 && shortcuts.All(r => r[1] == "RMPCS.ico" && r[2] == "0"), "installer has branded Start Menu and desktop shortcuts");
        Check(properties["ARPPRODUCTICON"] == "RMPCS.ico" && db.Query("SELECT `Name` FROM `Icon`").Single()[0] == "RMPCS.ico" && db.Query("SELECT `Name` FROM `Binary`").Single()[0] == "BrandIcon", "installer embeds branding for dialogs and Installed apps");
        var options = db.Query("SELECT `Condition`,`Attributes` FROM `Component` WHERE `Component`='ShortcutDesktop' OR `Component`='ShortcutStartMenu'");
        Check(options.Count == 2 && options.All(r => r[0].EndsWith("= \"1\"") && r[1] == "324"), "shortcut choices use transitive registry-keyed x64 components");
        Check(db.Query("SELECT `Name`,`Value` FROM `Registry` WHERE `Registry`='OptionAlwaysAdmin'").Single()[1] == "[ALWAYS_ADMIN]", "administrator preference is installed and removed by MSI");
        var ui = db.Query("SELECT `Action`,`Sequence` FROM `InstallUISequence`");
        Check(int.Parse(ui.Single(r => r[0] == "WelcomeDlg")[1]) < 800 && ui.Single(r => r[0] == "FinishDlg")[1] == "-1", "choices precede costing and completion page runs only after success");
        Check(db.Query("SELECT `Control_`,`Condition` FROM `ControlEvent` WHERE `Argument`='LaunchInstalledApp'").Single()[1] == "RUN_AFTER = \"1\"", "post-install launch requires the completion checkbox");
        Check(!sequence.Any(r => r[0] == "LaunchInstalledApp") && db.Query("SELECT `Type`,`Source` FROM `CustomAction` WHERE `Action`='LaunchInstalledApp'").Single()[0] == "210", "post-install launch is asynchronous and absent from unattended execution");
        var verifyFolder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(package))!, "verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(verifyFolder);
        string cabinet = Path.Combine(verifyFolder, "payload.cab"); db.ExtractCabinet(cabinet);
        var expand = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "expand.exe")) { UseShellExecute = false, CreateNoWindow = true };
        expand.ArgumentList.Add("-F:*"); expand.ArgumentList.Add(cabinet); expand.ArgumentList.Add(verifyFolder);
        using (var process = Process.Start(expand)!) { process.WaitForExit(); Check(process.ExitCode == 0, "embedded MSI cabinet extracts successfully"); }
        foreach (var row in fileRows)
        {
            string name = row[2].Split('|').Last();
            string source = name == "TaskCleanup.ps1" ? Path.GetFullPath(cleanup) : Path.Combine(Path.GetFullPath(app), name);
            string extracted = Path.Combine(verifyFolder, row[0]);
            Check(File.Exists(extracted) && new FileInfo(extracted).Length == long.Parse(row[3]) && SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(extracted))), "MSI payload matches published " + name);
        }
        Console.WriteLine("Offline MSI verification completed. Installation/uninstallation was not performed.");
    }
}
