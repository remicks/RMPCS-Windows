using System.Runtime.InteropServices;
using System.Text;

static class InstallerAudit
{
    internal static void Inspect(string path)
    {
        using var db = new MsiDatabase(Path.GetFullPath(path), true);
        foreach (var row in db.Query("SELECT `Property`,`Value` FROM `Property` WHERE `Property`='ProductVersion' OR `Property`='ProductCode' OR `Property`='UpgradeCode'"))
            Console.WriteLine(row[0] + ": " + row[1]);
        foreach (var row in db.Query("SELECT `VersionMin`,`VersionMax`,`Attributes`,`ActionProperty` FROM `Upgrade`"))
            Console.WriteLine("Upgrade: " + string.Join(" | ", row));
        Console.WriteLine("Welcome: " + db.Query("SELECT `Title` FROM `Dialog` WHERE `Dialog`='WelcomeDlg'").Single()[0]);
    }

    internal static void VerifyMetadata(MsiDatabase db, string version, Action<bool,string> check)
    {
        PackageVersion.Parse(version);
        var properties = db.Query("SELECT `Property`,`Value` FROM `Property`").ToDictionary(r => r[0], r => r[1]);
        check(properties["ProductVersion"] == version && properties["UpgradeCode"] == PackageVersion.UpgradeCode && Guid.TryParse(properties["ProductCode"], out _), "release version and upgrade-family identity are correct");
        var upgrades = db.Query("SELECT `UpgradeCode`,`VersionMin`,`VersionMax`,`Language`,`Attributes`,`Remove`,`ActionProperty` FROM `Upgrade`");
        check(upgrades.Count == 2 && upgrades.Single(r => r[6] == "OLDPRODUCTS").SequenceEqual(new[] { PackageVersion.UpgradeCode, "0.0.0", version, "1033", "257", "", "OLDPRODUCTS" }), "older-version detection includes legacy packages and excludes the current release");
        check(upgrades.Single(r => r[6] == "NEWERFOUND").SequenceEqual(new[] { PackageVersion.UpgradeCode, version, "", "1033", "258", "", "NEWERFOUND" }), "same/newer detection starts at the actual release version");
        var secure = properties["SecureCustomProperties"].Split(';');
        check(new[] { "OLDPRODUCTS", "NEWERFOUND", "CREATE_DESKTOP", "CREATE_STARTMENU", "ALWAYS_ADMIN", "DOTNETDESKTOP10", "DOTNETCORE10", "DOTNETHOST10" }.All(secure.Contains), "upgrade detection, runtime discovery and install choices cross the elevated MSI boundary");
        var conditions = db.Query("SELECT `Condition`,`Description` FROM `LaunchCondition`");
        check(conditions.Any(r => r[0] == "Installed OR NOT NEWERFOUND") && conditions.Any(r => r[0] == "Installed OR (DOTNETDESKTOP10 AND DOTNETCORE10 AND DOTNETHOST10)") && conditions.Any(r => r[0] == "VersionNT64"), "downgrade, complete-runtime and architecture conditions are retained");
        check(db.Query("SELECT `Title` FROM `Dialog` WHERE `Dialog`='WelcomeDlg'").Single()[0] == "RMPCS [ProductVersion] Setup" && db.Query("SELECT `Text` FROM `Control` WHERE `Dialog_`='WelcomeDlg' AND `Control`='Title'").Single()[0].Contains("[ProductVersion]"), "welcome title derives from ProductVersion rather than a stale literal");
        var execute = db.Query("SELECT `Action`,`Condition`,`Sequence` FROM `InstallExecuteSequence`").ToDictionary(r => r[0]);
        var ui = db.Query("SELECT `Action`,`Condition`,`Sequence` FROM `InstallUISequence`").ToDictionary(r => r[0]);
        int Position(Dictionary<string,string[]> rows, string key) => int.Parse(rows[key][2]);
        check(Position(execute,"FindRelatedProducts") < Position(execute,"LaunchConditions") && Position(ui,"FindRelatedProducts") < Position(ui,"LaunchConditions"), "related versions are detected before launch conditions in interactive and silent installs");
        check(Position(execute,"InstallInitialize") < Position(execute,"RemoveExistingProducts") && Position(execute,"RemoveExistingProducts") < Position(execute,"ProcessComponents") && Position(execute,"InstallFiles") < Position(execute,"InstallFinalize"), "major-upgrade removal is inside the rollback transaction before new components are processed");
        check(!execute.ContainsKey("DisableRollback") && !execute.ContainsKey("ForceReboot") && !execute.ContainsKey("ScheduleReboot"), "MSI rollback is enabled and no automatic restart action is authored");
        check(ui["MaintenanceDlg"][1] == "Installed AND NOT REMOVE AND NOT REINSTALL AND NOT UPGRADINGPRODUCTCODE" && Position(ui,"MaintenanceDlg") < Position(ui,"CostInitialize"), "same-product repair/remove choices are made before costing");
        check(ui["RemoveDlg"][1] == "Installed AND REMOVE~=\"ALL\" AND NOT UPGRADINGPRODUCTCODE" && db.Query("SELECT `Control_First`,`Control_Default`,`Control_Cancel` FROM `Dialog` WHERE `Dialog`='RemoveDlg'").Single().All(v=>v=="Cancel"), "removal requires an explicit request with Cancel focused, default and Escape action");
        var repair = db.Query("SELECT `Event`,`Argument`,`Ordering` FROM `ControlEvent` WHERE `Dialog_`='MaintenanceDlg' AND `Control_`='Continue'");
        check(repair.Single(r => r[0] == "[REINSTALL]")[1] == "ALL" && repair.Single(r => r[0] == "[REINSTALLMODE]")[1] == "omus" && repair.Where(r => r[0] != "EndDialog").All(r => int.Parse(r[2]) < int.Parse(repair.Single(e => e[0] == "EndDialog")[2])), "Repair sets standard reinstall options before returning to the engine");
        check(execute["CleanupTasks"][1] == "REMOVE~=\"ALL\" AND NOT UPGRADINGPRODUCTCODE", "only explicit uninstall removes RMPCS tasks; major upgrades preserve them");
        check(ui["FinishDlg"][1] == "NOT Installed AND NOT REMOVE" && ui["FinishDlg"][2] == "-1" && ui["RemovedDlg"][2] == "-1" && ui["CancelledDlg"][2] == "-2" && ui["FailedDlg"][2] == "-3", "install, repair, remove, cancel and failure have distinct outcome routing");
        var events = db.Query("SELECT `Dialog_`,`Control_`,`Event`,`Argument`,`Condition` FROM `ControlEvent` WHERE `Event`='DoAction'");
        var expectedLaunches=new Dictionary<string,string>{["LaunchInstalledApp"]="RUN_AFTER = \"1\" AND SENSOR_SETUP <> \"1\" AND NOT ReplacedInUseFiles AND NOT MsiSystemRebootPending",["LaunchSensorSetup"]="SENSOR_SETUP = \"1\" AND RUN_AFTER <> \"1\" AND NOT ReplacedInUseFiles AND NOT MsiSystemRebootPending",["LaunchSensorSetupAndApp"]="SENSOR_SETUP = \"1\" AND RUN_AFTER = \"1\" AND NOT ReplacedInUseFiles AND NOT MsiSystemRebootPending"};
        check(events.Count==3&&expectedLaunches.All(pair=>events.Count(e=>e.SequenceEqual(new[]{"FinishDlg","Finish","DoAction",pair.Key,pair.Value}))==1&&!execute.ContainsKey(pair.Key)),"only explicitly selected success-Finish actions can launch app/sensor workflows; unattended execution and pending restart are blocked");
        // This ordering follows Microsoft's documented ExitDialog/Launch sample.
        var finish = db.Query("SELECT `Event`,`Ordering` FROM `ControlEvent` WHERE `Dialog_`='FinishDlg' AND `Control_`='Finish'");
        check(finish.Where(r=>r[0]=="DoAction").All(r=>int.Parse(finish.Single(e=>e[0]=="EndDialog")[1])<int.Parse(r[1])), "post-install launches follow the documented EndDialog then DoAction ordering");
    }

    internal static void Run(string package, string app, string cleanup, string version)
    {
        int count = 0;
        void Check(bool ok, string description) { if (!ok) throw new InvalidDataException(description); count++; Console.WriteLine("PASS: " + description); }
        PackageVersion.RequireExpected(app, version);
        foreach (string invalid in new[] { "0.12", "0.12.1.0", " 0.12.1", "00.12.1", "256.0.0", "0.256.0", "0.0.65536", "0.12.1-preview", "$(run)" })
        {
            bool rejected = false; try { PackageVersion.Parse(invalid); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "invalid release version rejected: " + invalid);
        }
        Check(PackageVersion.Parse("255.255.65535").ToString(3) == "255.255.65535", "MSI's three-field version limits are enforced");
        using (var db = new MsiDatabase(Path.GetFullPath(package), true))
        {
            VerifyMetadata(db, version, Check);
            var rows = db.Query("SELECT `VersionMin`,`VersionMax`,`Attributes`,`ActionProperty` FROM `Upgrade`");
            bool Matches(string value, string[] row)
            {
                var v = PackageVersion.Parse(value); int flags = int.Parse(row[2]);
                return (row[0] == "" || (v.CompareTo(PackageVersion.Parse(row[0])) is var min && (min > 0 || min == 0 && (flags & 256) != 0))) &&
                       (row[1] == "" || (v.CompareTo(PackageVersion.Parse(row[1])) is var max && (max < 0 || max == 0 && (flags & 512) != 0)));
            }
            var current = PackageVersion.Parse(version);
            foreach (var installed in new[] { ("0.0.0", true), ("0.8.0", true), ("0.9.2", true), ("0.10.1", true), ("0.11.0", true), ("0.12.0", true), (version, false), ($"{current.Major}.{current.Minor}.{current.Build + 1}", false), ("255.255.65535", false) })
            {
                Check(Matches(installed.Item1, rows.Single(r => r[3] == "OLDPRODUCTS")) == installed.Item2 && Matches(installed.Item1, rows.Single(r => r[3] == "NEWERFOUND")) != installed.Item2, "upgrade boundary classification: installed " + installed.Item1);
            }
            VerifyConditions(package, db, Check);
        }
        var folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(package))!, "installer-regressions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var mutations = new[]
        {
            ("sensor-without-opt-in", "DELETE FROM `ControlEvent` WHERE `Argument`='LaunchSensorSetup'"),
            ("sensor-unattended-execution", "INSERT INTO `InstallExecuteSequence` (`Action`,`Condition`,`Sequence`) VALUES ('LaunchSensorSetup','1',6700)"),
            ("stale-product-version", "UPDATE `Property` SET `Value`='0.9.2' WHERE `Property`='ProductVersion'"),
            ("stale-old-product-boundary", "DELETE FROM `Upgrade` WHERE `ActionProperty`='OLDPRODUCTS'"),
            ("stale-newer-boundary", "DELETE FROM `Upgrade` WHERE `ActionProperty`='NEWERFOUND'"),
            ("stale-welcome-version", "UPDATE `Dialog` SET `Title`='RMPCS 0.9.2 Setup' WHERE `Dialog`='WelcomeDlg'"),
            ("missing-downgrade-guard", "DELETE FROM `LaunchCondition` WHERE `Condition`='Installed OR NOT NEWERFOUND'"),
            ("partial-runtime-bypass", "DELETE FROM `LaunchCondition` WHERE `Condition`='Installed OR (DOTNETDESKTOP10 AND DOTNETCORE10 AND DOTNETHOST10)'"),
            ("unsafe-upgrade-removal", "UPDATE `InstallExecuteSequence` SET `Sequence`=1499 WHERE `Action`='RemoveExistingProducts'"),
            ("repair-removes-app", "UPDATE `InstallUISequence` SET `Condition`='Installed AND NOT REINSTALL' WHERE `Action`='RemoveDlg'")
        };
        foreach (var (name, sql) in mutations)
        {
            string file = Path.Combine(folder, name + ".msi"); File.Copy(package, file);
            try
            {
                using (var edited = new MsiDatabase(file, update: true))
                {
                    edited.Sql(sql);
                    if(name=="sensor-without-opt-in")edited.Row("ControlEvent","FinishDlg","Finish","DoAction","LaunchSensorSetup","1",3);
                    if (name == "stale-old-product-boundary") edited.Row("Upgrade", PackageVersion.UpgradeCode, "0.0.0", "0.9.2", "1033", 257, null, "OLDPRODUCTS");
                    if (name == "stale-newer-boundary") edited.Row("Upgrade", PackageVersion.UpgradeCode, "0.9.2", null, "1033", 258, null, "NEWERFOUND");
                    if (name == "partial-runtime-bypass") edited.Row("LaunchCondition", "Installed OR DOTNETDESKTOP10", "Broken prerequisite fixture");
                    edited.Commit();
                }
                bool rejected = false;
                try { using var edited = new MsiDatabase(file, true); VerifyMetadata(edited, version, (ok, message) => { if (!ok) throw new InvalidDataException(message); }); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected, "corrupted installer regression is rejected: " + name);
            }
            finally { File.Delete(file); }
        }
        Directory.Delete(folder, false);
        bool mismatch = false; try { PackageVersion.RequireExpected(app, "0.0.0"); } catch (InvalidDataException) { mismatch = true; }
        Check(mismatch, "release manifest/application mismatch fails closed");
        Console.WriteLine($"{count} installer version, upgrade, native-condition and corruption-regression checks passed. No installation, repair, removal or custom action executed.");
    }

    private static void VerifyConditions(string package, MsiDatabase db, Action<bool,string> check)
    {
        // Microsoft documents flag 1 as incapable of changing machine state.
        uint oldUi = MsiSetInternalUI(2, 0); // No dialogs during restricted-session checks.
        uint session = 0;
        try
        {
            MsiNative.Check(MsiOpenPackageExW(Path.GetFullPath(package), 1, out session), "Open restricted, machine-independent MSI session");
            uint titleRecord = MsiNative.MsiCreateRecord(0);
            try
            {
                MsiNative.Check(MsiNative.MsiRecordSetStringW(titleRecord, 0, db.Query("SELECT `Title` FROM `Dialog` WHERE `Dialog`='WelcomeDlg'").Single()[0]), "Set title-format fixture");
                uint length = 0; uint code = MsiFormatRecordW(session, titleRecord, null, ref length);
                if (code is not (0 or 234)) MsiNative.Check(code, "Measure formatted title");
                var title = new StringBuilder((int)length + 1); length = (uint)title.Capacity;
                MsiNative.Check(MsiFormatRecordW(session, titleRecord, title, ref length), "Format title through Windows Installer");
                string version = db.Query("SELECT `Value` FROM `Property` WHERE `Property`='ProductVersion'").Single()[0];
                check(title.ToString() == "RMPCS " + version + " Setup", "Windows Installer resolves the welcome title to the actual product version");
            }
            finally { MsiNative.MsiCloseHandle(titleRecord); }
            string[] fields = ["Installed", "OLDPRODUCTS", "NEWERFOUND", "REMOVE", "REINSTALL", "UPGRADINGPRODUCTCODE", "DOTNETDESKTOP10", "DOTNETCORE10", "DOTNETHOST10", "RUN_AFTER", "SENSOR_SETUP", "ReplacedInUseFiles", "MsiSystemRebootPending", "CREATE_DESKTOP", "CREATE_STARTMENU", "ALWAYS_ADMIN", "PREVIOUS_CREATE_DESKTOP", "PREVIOUS_CREATE_STARTMENU", "PREVIOUS_ALWAYS_ADMIN"];
            var ui = db.Query("SELECT `Action`,`Condition` FROM `InstallUISequence`").ToDictionary(r => r[0], r => r[1]);
            var runtime = db.Query("SELECT `Condition` FROM `LaunchCondition`").Single(r=>r[0].Contains("DOTNETDESKTOP10"))[0];
            var downgrade = db.Query("SELECT `Condition` FROM `LaunchCondition` WHERE `Condition`='Installed OR NOT NEWERFOUND'").Single()[0];
            void Set(string key, string? value) => MsiNative.Check(MsiSetPropertyW(session, key, value), "Set fixture property " + key);
            bool Eval(string expression) { int result = MsiEvaluateConditionW(session, expression); if (result == 3) throw new InvalidDataException("Invalid MSI condition: " + expression); return result is 1 or 2; }
            void Reset() { foreach (string field in fields) Set(field, null); }
            Reset(); foreach(string component in new[]{"DOTNETDESKTOP10","DOTNETCORE10","DOTNETHOST10"})Set(component,"ready");
            check(Eval(runtime) && Eval(downgrade) && Eval(ui["WelcomeDlg"]) && !Eval(ui["MaintenanceDlg"]) && !Eval(ui["RemoveDlg"]) && Eval(ui["FinishDlg"]), "native engine routes fresh install to welcome and success-only Finish");
            Set("OLDPRODUCTS", "{00000000-0000-0000-0000-000000000001}");
            check(Eval(downgrade) && Eval(ui["WelcomeDlg"]) && !Eval(ui["RemoveDlg"]), "native engine allows upgrades from a related older package");
            Set("NEWERFOUND", "{00000000-0000-0000-0000-000000000002}"); check(!Eval(downgrade), "native engine blocks equal/newer related packages");
            Reset(); check(!Eval(runtime), "missing runtime blocks a fresh installation");
            foreach(string missing in new[]{"DOTNETDESKTOP10","DOTNETCORE10","DOTNETHOST10"})
            {
                foreach(string component in new[]{"DOTNETDESKTOP10","DOTNETCORE10","DOTNETHOST10"})Set(component,component==missing?null:"ready");
                check(!Eval(runtime), "partial runtime blocks fresh installation: missing "+missing);
            }
            Reset();
            Set("Installed", "1"); check(Eval(runtime) && Eval(ui["MaintenanceDlg"]) && !Eval(ui["RemoveDlg"]) && !Eval(ui["FinishDlg"]), "installed product offers maintenance without requiring the runtime for removal");
            Set("REINSTALL", "ALL"); check(!Eval(ui["MaintenanceDlg"]) && !Eval(ui["RemoveDlg"]) && !Eval(ui["FinishDlg"]), "explicit repair bypasses removal and post-install launch");
            Set("REINSTALL", null); Set("REMOVE", "ALL"); check(Eval(ui["RemoveDlg"]) && !Eval(ui["MaintenanceDlg"]) && !Eval(ui["FinishDlg"]), "explicit uninstall reaches removal confirmation only");
            string cleanup = db.Query("SELECT `Condition` FROM `InstallExecuteSequence` WHERE `Action`='CleanupTasks'").Single()[0];
            check(Eval(cleanup), "explicit uninstall selects task cleanup");
            Set("UPGRADINGPRODUCTCODE", "upgrading"); check(!Eval(cleanup) && !Eval(ui["RemoveDlg"]), "concurrent upgrade removal preserves automation and avoids a second removal dialog");
            string launch = db.Query("SELECT `Condition` FROM `ControlEvent` WHERE `Argument`='LaunchInstalledApp'").Single()[0];
            Reset(); Set("RUN_AFTER", "0"); check(!Eval(launch), "unchecked Finish never selects app launch"); Set("RUN_AFTER", "1"); check(Eval(launch), "checked Finish selects app launch only on its success dialog");
            Set("ReplacedInUseFiles", "1"); check(!Eval(launch), "in-use replacement postpones post-install launch until restart");
            Set("ReplacedInUseFiles", null); Set("MsiSystemRebootPending", "1"); check(!Eval(launch), "pending system restart suppresses post-install launch");
            string sensorOnly=db.Query("SELECT `Condition` FROM `ControlEvent` WHERE `Argument`='LaunchSensorSetup'").Single()[0],sensorAndApp=db.Query("SELECT `Condition` FROM `ControlEvent` WHERE `Argument`='LaunchSensorSetupAndApp'").Single()[0];
            foreach(bool sensors in new[]{false,true})foreach(bool run in new[]{false,true})foreach(bool restart in new[]{false,true})
            {
                Reset();Set("SENSOR_SETUP",sensors?"1":null);Set("RUN_AFTER",run?"1":null);Set("MsiSystemRebootPending",restart?"1":null);
                check(Eval(launch)==(run&&!sensors&&!restart)&&Eval(sensorOnly)==(sensors&&!run&&!restart)&&Eval(sensorAndApp)==(sensors&&run&&!restart),"native Finish routes exactly one requested workflow and suppresses all launches for pending restarts");
            }
            Reset();Set("SENSOR_SETUP","1");Set("ReplacedInUseFiles","1");check(!Eval(sensorOnly)&&!Eval(sensorAndApp),"in-use file replacement suppresses optional sensor helpers");
            foreach (int mask in Enumerable.Range(0, 8))
            {
                Set("CREATE_DESKTOP", (mask & 1) != 0 ? "1" : "0"); Set("CREATE_STARTMENU", (mask & 2) != 0 ? "1" : "0"); Set("ALWAYS_ADMIN", (mask & 4) != 0 ? "1" : "0");
                check(Eval("CREATE_DESKTOP = \"1\"") == ((mask & 1) != 0) && Eval("CREATE_STARTMENU = \"1\"") == ((mask & 2) != 0) && Eval("ALWAYS_ADMIN = \"1\"") == ((mask & 4) != 0) && !Eval("NOT CREATE_DESKTOP"), "native engine preserves explicit 0/1 choices: " + mask);
            }
        }
        finally { if (session != 0) MsiNative.MsiCloseHandle(session); MsiSetInternalUI(oldUi, 0); }
    }
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint MsiOpenPackageExW(string path, uint options, out uint session);
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint MsiSetPropertyW(uint session, string name, string? value);
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int MsiEvaluateConditionW(uint session, string condition);
    [DllImport("msi.dll")] private static extern uint MsiSetInternalUI(uint level, nint owner);
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint MsiFormatRecordW(uint session, uint record, StringBuilder? value, ref uint length);
}
