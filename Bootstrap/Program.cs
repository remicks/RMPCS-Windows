using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyProduct("RMPCS")]
[assembly: AssemblyCompany("Degenerates Inc.")]
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace RMPCS.Bootstrap
{
    internal static class Program
    {
        internal static string VersionLabel { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } }
        internal static bool SameProductInstalled() { return BootstrapPolicy.RequiresRepair(MsiQueryProductStateW(PackageIdentity.ProductCode)); }
        [DllImport("msi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] private static extern int MsiQueryProductStateW(string productCode);
        internal static bool Installer {
            get {
#if INSTALLER
                return true;
#else
                return false;
#endif
            }
        }
        internal static Dictionary<string, string> Manifest()
        { return Manifest("PayloadManifest"); }
        private static Dictionary<string,string> Manifest(string resource)
        { using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) using (var reader = new StreamReader(stream)) return BootstrapPolicy.ParseManifest(reader.ReadToEnd()); }
        internal static void ProtectedPath(string path)
        {
            for(string current=path;current!=null;current=Path.GetDirectoryName(current))
            {
                if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked installation paths cannot launch elevated sensor setup.");
                FileSystemSecurity acl=Directory.Exists(current)?(FileSystemSecurity)new DirectoryInfo(current).GetAccessControl():new FileInfo(current).GetAccessControl();
                Func<IdentityReference,bool> trusted=delegate(IdentityReference sid){return sid!=null&&(sid.Value=="S-1-5-32-544"||sid.Value=="S-1-5-18"||sid.Value=="S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");};
                if(!trusted(acl.GetOwner(typeof(SecurityIdentifier))))throw new IOException("Installation path owner is not protected.");
                foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
                    if(rule.AccessControlType==AccessControlType.Allow&&(rule.PropagationFlags&PropagationFlags.InheritOnly)==0&&!trusted(rule.IdentityReference)&&(rule.FileSystemRights&(FileSystemRights.Write|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership))!=0)throw new IOException("Installation path allows non-administrator changes.");
                if(String.Equals(current,Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),StringComparison.OrdinalIgnoreCase))break;
            }
        }
        internal static HeldPayload OpenInstalledSensorPayload(string root)
        {
            var held=new List<FileStream>();
            try
            {
                ProtectedPath(root);
                foreach(var entry in Manifest("InstalledSensorManifest"))
                {
                    string path=Path.Combine(root,entry.Key);ProtectedPath(path);
                    var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);held.Add(stream);
                    if(BootstrapPolicy.Hash(stream,false)!=entry.Value)throw new InvalidDataException("Installed sensor-setup helper integrity check failed. Use Settings after repairing RMPCS.");
                }
                return new HeldPayload(held);
            }
            catch{foreach(var stream in held)stream.Dispose();throw;}
        }
        internal static bool RuntimePresent()
        {
            string registered = null;
            try { using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) using (var key = machine.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64")) registered = key == null ? null : key.GetValue("InstallLocation") as string; } catch (System.Security.SecurityException) { }
            // .NET registers x64 installation discovery in the 32-bit registry view.
            // The MSI uses that same registration. Portable apphost lookup also respects
            // process environment overrides, then registration, then the default root.
            string root = registered;
            if (!Installer)
            {
                string custom = Environment.GetEnvironmentVariable("DOTNET_ROOT_X64");
                if (String.IsNullOrEmpty(custom)) custom = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                if (!String.IsNullOrEmpty(custom)) root = custom;
                if (String.IsNullOrEmpty(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
            }
            if (String.IsNullOrEmpty(root)) return false;
            try { return HasFramework(Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App"), "PresentationFramework.dll") && HasFramework(Path.Combine(root, "shared", "Microsoft.NETCore.App"), "coreclr.dll") && HasFramework(Path.Combine(root, "host", "fxr"), "hostfxr.dll"); }
            catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; } catch (ArgumentException) { return false; }
        }
        private static bool HasFramework(string root, string file)
        { if (!Directory.Exists(root)) return false; foreach (string path in Directory.GetDirectories(root)) if (BootstrapPolicy.CompatibleVersion(Path.GetFileName(path)) && File.Exists(Path.Combine(path, file))) return true; return false; }
        internal static List<FileStream> OpenVerifiedPayload()
        {
            var held = new List<FileStream>();
            try
            {
                foreach (var entry in Manifest())
                {
                    var file = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, entry.Key), FileMode.Open, FileAccess.Read, FileShare.Read);
                    held.Add(file); if (BootstrapPolicy.Hash(file, false) != entry.Value) throw new InvalidDataException("Payload integrity check failed: " + entry.Key);
                }
                return held;
            }
            catch { foreach (var file in held) file.Dispose(); throw; }
        }
        [STAThread]
        private static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--self-test") >= 0)
            {
                try
                {
                    int count = SelfTests();
                    if (args.Length == 2) File.WriteAllText(args[1], count + " bootstrap checks passed. No downloads, installation or application launch performed.");
                    return 0;
                }
                catch (Exception ex) { if (args.Length == 2) File.WriteAllText(args[1], ex.ToString()); return 1; }
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool smoke = Array.IndexOf(args, "--smoke-test") >= 0;
            bool progressCheck=args.Length==2&&args[0]=="--progress-ui-check";
            bool preview = args.Length == 2 && (args[0] == "--ui-preview" || args[0] == "--ui-preview-options" || args[0] == "--ui-preview-finish" || args[0] == "--ui-preview-progress");
            try
            {
                if (!Installer && !smoke && !preview && !progressCheck && Array.IndexOf(args, "--setup") < 0 && RuntimePresent())
                {
                    using (var ownership = new HeldPayload(OpenVerifiedPayload()))
                    { using (var app = Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RMPCS.exe")) { UseShellExecute = false, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory })) app.WaitForExit(); }
                    return 0;
                }
                int checkExit=0;
                using (var form = new SetupForm())
                {
                    if(progressCheck)form.Shown+=delegate {form.BeginInvoke((Action)(async delegate {
                        try {int count=await form.VerifyProgressUiAsync();File.WriteAllText(args[1],count+" setup progress checks passed. No downloads, installation, removal, upgrade or app launch performed.");}
                        catch(Exception ex){checkExit=1;File.WriteAllText(args[1],ex.ToString());}
                        finally {form.EndProgressCheck();form.Close();}
                    }));};
                    if(preview && args[0]=="--ui-preview-options")form.PreviewPage(1);
                    if(preview && args[0]=="--ui-preview-finish")form.PreviewPage(3);
                    if(preview && args[0]=="--ui-preview-progress")form.PreviewPage(2);
                    if (smoke || preview) form.Shown += delegate { form.BeginInvoke((Action)delegate {
                        if (!smoke) using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0,0,form.Width,form.Height)); bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png); }
                        form.Close();
                    }); };
                    Application.Run(form);
                }
                return checkExit;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "RMPCS setup could not continue", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
        private static int SelfTests()
        {
            int count = 0;
            Action<bool> check = delegate(bool ok) { if (!ok) throw new InvalidDataException("Bootstrap self-test failed at " + (count + 1)); count++; };
            check(BootstrapPolicy.CompatibleVersion("10.0.15")); check(!BootstrapPolicy.CompatibleVersion("9.0.15"));
            check(!BootstrapPolicy.CompatibleVersion("11.0.0")); check(!BootstrapPolicy.CompatibleVersion("10.0.0-preview.1"));
            check(BootstrapPolicy.RuntimeSha512.Length == 128 && BootstrapPolicy.IsHex(BootstrapPolicy.RuntimeSha512));
            check(new Uri(BootstrapPolicy.RuntimeUrl).Scheme == "https" && new Uri(BootstrapPolicy.RuntimeUrl).Host == "builds.dotnet.microsoft.com");
            check(BootstrapPolicy.AcceptedExit(0) && BootstrapPolicy.AcceptedExit(3010) && !BootstrapPolicy.AcceptedExit(1602) && !BootstrapPolicy.AcceptedExit(1641));
            check(BootstrapPolicy.RequiresRepair(5)&&!BootstrapPolicy.RequiresRepair(-1)&&!BootstrapPolicy.RequiresRepair(0)&&!BootstrapPolicy.RequiresRepair(1));
            check(Guid.Parse(PackageIdentity.ProductCode).ToString("B").ToUpperInvariant()==PackageIdentity.ProductCode);
            for(int mask=0;mask<8;mask++)
            {
                string arguments=SetupChoices.MsiArguments(@"C:\safe path\RMPCS.msi",(mask&1)!=0,(mask&2)!=0,(mask&4)!=0);
                check(arguments.Contains("CREATE_DESKTOP="+((mask&1)!=0?"1":"0")) && arguments.Contains("CREATE_STARTMENU="+((mask&2)!=0?"1":"0")) && arguments.Contains("ALWAYS_ADMIN="+((mask&4)!=0?"1":"0")) && !arguments.Contains("RUN_AFTER"));
                check(!arguments.Contains("REINSTALL")&&SetupChoices.MsiArguments(@"C:\safe path\RMPCS.msi",(mask&1)!=0,(mask&2)!=0,(mask&4)!=0,true).EndsWith(" REINSTALL=ALL REINSTALLMODE=omus"));
            }
            bool badPath=false;try{SetupChoices.MsiArguments("bad\"path",true,true,true);}catch(ArgumentException){badPath=true;}check(badPath);
            bool rejected = false; try { BootstrapPolicy.ParseManifest("../bad.exe|" + new string('a', 64)); } catch (InvalidDataException) { rejected = true; } check(rejected);
            using (var bytes = new MemoryStream(new byte[] {1,2,3})) check(BootstrapPolicy.Hash(bytes, true) != BootstrapPolicy.RuntimeSha512);
            bool signatureRejected = false;
            string self = Assembly.GetExecutingAssembly().Location;
            using (var heldSelf = new FileStream(self, FileMode.Open, FileAccess.Read, FileShare.Read))
                try { Signature.VerifyMicrosoft(self, heldSelf.SafeFileHandle.DangerousGetHandle()); } catch (InvalidDataException) { signatureRejected = true; }
            check(signatureRejected);
            check(!SetupChoices.OfferSensors(false,0,false)&&SetupChoices.OfferSensors(true,0,false));
            check(!SetupChoices.OfferSensors(true,3010,false)&&!SetupChoices.OfferSensors(true,0,true)&&!SetupChoices.OfferSensors(true,1602,false)&&!SetupChoices.OfferSensors(true,1603,false));
            if (Installer)
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InstallerPayload")) check(BootstrapPolicy.Hash(stream, false) == Manifest()["RMPCS.msi"]);
                var helper=Manifest("InstalledSensorManifest");check(helper.ContainsKey("RMPCS.exe")&&helper.ContainsKey("RMPCS.dll")&&helper.ContainsKey("RMPCS.Core.dll")&&helper.ContainsKey("RMPCS.runtimeconfig.json"));
            }
            else { using (var held = new HeldPayload(OpenVerifiedPayload())) check(held.Count >= 5); }
            return count;
        }
        internal sealed class HeldPayload : IDisposable
        { private readonly List<FileStream> streams; public int Count { get { return streams.Count; } } public HeldPayload(List<FileStream> streams) { this.streams = streams; } public void Dispose() { foreach (var stream in streams) stream.Dispose(); } }
    }

    internal sealed partial class SetupForm : Form
    {
        private readonly Label status = new Label();
        private readonly CheckBox prerequisite = new CheckBox();
        private readonly Button proceed = new Button(), offline = new Button(), cancel = new Button();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool busy, installing, restartRequired;
        private string offlinePath;
        private async Task ContinueAsync()
        {
            if (busy) return;
            if (!Program.RuntimePresent() && !prerequisite.Checked) { MessageBox.Show(this, "Install the Desktop Runtime manually using the Microsoft link, then try again. No changes were made."); return; }
            if (!Program.RuntimePresent() && MessageBox.Show(this, "Download/use the selected Microsoft .NET 10 Desktop Runtime installer and launch it? This installs a shared system component. Follow Microsoft's setup and license prompts. RMPCS will not request an automatic restart.", "Install prerequisite", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            busy = true; Stage("Preparing setup", "Preparing verified installation files..."); back.Visible=false; proceed.Enabled = offline.Enabled = prerequisite.Enabled = false;
            string temp = null;
            bool sessionCompleted=false;
            try
            {
                temp = PrivateDirectory();
                if (!Program.RuntimePresent())
                {
                    string runtime = Path.Combine(temp, "MicrosoftDesktopRuntime.exe");
                    Stage("Preparing the Microsoft runtime", "Preparing the required Microsoft Desktop Runtime installer...");
                    if (offlinePath == null) await DownloadAsync(runtime);
                    else { using (var source = new FileStream(offlinePath, FileMode.Open, FileAccess.Read, FileShare.Read)) { if (source.Length <= 0 || source.Length > 200L * 1024 * 1024) throw new IOException("Unexpected offline runtime installer size."); using (var dest = new FileStream(runtime, FileMode.CreateNew, FileAccess.Write, FileShare.None)) await source.CopyToAsync(dest); } }
                    using (var held = new FileStream(runtime, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        Stage("Verifying the Microsoft runtime", "Checking the runtime checksum and Microsoft Authenticode trust...");
                        await Task.Run((Action)delegate {
                            if (BootstrapPolicy.Hash(held, true) != BootstrapPolicy.RuntimeSha512) throw new InvalidDataException("Runtime checksum mismatch. Nothing was executed. Choose Microsoft's exact 10.0.15 Windows x64 Desktop Runtime installer or retry the official download.");
                            Signature.VerifyMicrosoft(runtime, held.SafeFileHandle.DangerousGetHandle());
                        });
                        operationHeading.Text="Installing the Microsoft runtime";
                        int code = await ExecuteAsync(runtime, "/install /norestart", true);
                        if (!BootstrapPolicy.AcceptedExit(code)) throw new InvalidOperationException(code == 1602 ? "Runtime installation was cancelled." : "Runtime installer returned " + code + ". Setup stopped.");
                        if (code == 3010) { restartRequired=true; MessageBox.Show(this, "Microsoft reports that a restart is required. Save your work and restart later. RMPCS will not schedule a restart or automatically open the app before that restart.", "Restart needed"); }
                    }
                    if (!Program.RuntimePresent()) throw new InvalidOperationException("The required runtime is not available yet. Restart if Microsoft requested it, then retry. A custom DOTNET_ROOT override can also prevent portable runtime discovery; installing a system runtime does not change that override.");
                }
                if (Program.Installer)
                {
                    Stage("Preparing RMPCS", "Extracting and verifying the embedded Windows Installer package...");
                    string msi = Path.Combine(temp, "RMPCS.msi");
                    using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("InstallerPayload")) using (var dest = new FileStream(msi, FileMode.CreateNew, FileAccess.Write, FileShare.None)) await source.CopyToAsync(dest);
                    using (var held = new FileStream(msi, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await Task.Run((Action)delegate {if (BootstrapPolicy.Hash(held, false) != Program.Manifest()["RMPCS.msi"]) throw new InvalidDataException("Embedded installer integrity check failed.");});
                        bool repair=Program.SameProductInstalled();
                        operationHeading.Text=repair?"Repairing RMPCS":File.Exists(InstalledExe())?"Upgrading RMPCS":"Installing RMPCS";
                        int code = await ExecuteAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe"), SetupChoices.MsiArguments(msi,desktopChoice.Checked,menuChoice.Checked,adminChoice.Checked,repair), false);
                        if (!BootstrapPolicy.AcceptedExit(code)) throw new InvalidOperationException(code == 1602 ? "RMPCS installation was cancelled." : "Windows Installer returned " + code + ". Review its message and retry.");
                        if(sensorChoice.Checked)
                        {
                            sensorSummary="Optional sensors: deferred; retry from app Settings after restarting.";
                            if(SetupChoices.OfferSensors(true,code,restartRequired))
                            {
                                Stage("Optional temperature sensor setup","RMPCS is installed. Verifying the installed helper before opening its separate review window...");
                                try
                                {
                                    using(var helper=Program.OpenInstalledSensorPayload(Path.GetDirectoryName(InstalledExe())))
                                    {
                                        int sensors=await ExecuteAsync(InstalledExe(),"--sensor-setup",true,true);
                                        if(sensors==3010)restartRequired=true;
                                        sensorSummary=sensors==0?"Optional sensors: components verified; enable reads in app Settings.":sensors==3010?"Optional sensors: restart required; monitoring stays off.":"Optional sensors: skipped/unfinished; retry from app Settings.";
                                    }
                                }
                                catch(Exception ex){sensorSummary="Optional sensors: not completed; retry from app Settings.";MessageBox.Show(this,"RMPCS was installed successfully. Optional sensor setup could not complete. Use Settings > Realtime system overlay > Temperature sources later.\n\n"+ex.Message,"Optional sensor setup",MessageBoxButtons.OK,MessageBoxIcon.Information);}
                            }
                        }
                        CompleteSetup(code == 3010 || restartRequired ? 3010 : 0);
                    }
                }
                else
                {
                    if (restartRequired) throw new InvalidOperationException("The Microsoft runtime was installed but requires a Windows restart. Restart Windows, then open Launch-RMPCS.exe again. RMPCS was not launched.");
                    Stage("RMPCS is opening", "Verifying the portable application. This launcher keeps its payload files protected until the app closes.");
                    int code;
                    using (var held = new Program.HeldPayload(Program.OpenVerifiedPayload())) code=await ExecuteAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RMPCS.exe"), "", false);
                    if(code!=0)throw new InvalidOperationException("RMPCS closed with exit code "+code+". Review the application before retrying.");
                    status.Text = "RMPCS session finished.";
                    sessionCompleted=true;
                }
            }
            catch (OperationCanceledException) { status.Text = "Setup was cancelled or timed out before the next launch. Any earlier completed prerequisite installation is kept. Close and reopen setup to retry."; }
            catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Setup did not complete", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally
            {
                installing = busy = false; cancel.Enabled = true; cancel.Text="Close";
                if(completed){progress.Style=ProgressBarStyle.Blocks;progress.Value=100;}else if(sessionCompleted)PortableSessionFinished();else OperationDidNotComplete();
                proceed.Enabled = !cancellation.IsCancellationRequested; offline.Enabled = !Program.RuntimePresent() && !cancellation.IsCancellationRequested;
                prerequisite.Enabled = !Program.RuntimePresent(); progress.Style = ProgressBarStyle.Blocks;
                if (temp != null) { foreach (string name in new[] { "MicrosoftDesktopRuntime.exe", "RMPCS.msi" }) try { File.Delete(Path.Combine(temp, name)); } catch (IOException) { } catch (UnauthorizedAccessException) { } try { Directory.Delete(temp, false); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
        private async Task DownloadAsync(string path)
        {
            // TLS 1.2 minimum; also allow TLS 1.3 where Windows supports it (Tls12 alone disabled 1.3).
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false }) using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(15) })
            {
                deadline.CancelAfter(TimeSpan.FromMinutes(15));
                using (var response = await client.GetAsync(BootstrapPolicy.RuntimeUrl, HttpCompletionOption.ResponseHeadersRead, deadline.Token))
                {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 200L * 1024 * 1024) throw new IOException("Unexpected runtime download size.");
                using (var source = await response.Content.ReadAsStreamAsync()) using (var dest = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920]; long total = 0; int count;
                    while ((count = await source.ReadAsync(buffer, 0, buffer.Length, deadline.Token)) > 0)
                    {
                        total += count; if (total > 200L * 1024 * 1024) throw new IOException("Runtime download exceeded the size limit.");
                        await dest.WriteAsync(buffer, 0, count, deadline.Token); status.Text = "Downloading from Microsoft: " + (total / 1048576d).ToString("N1") + " MB. You can cancel before installation.";
                        long? length=response.Content.Headers.ContentLength;
                        if(length.HasValue&&length.Value>0){progress.Style=ProgressBarStyle.Blocks;progress.Value=(int)Math.Min(100,100d*total/length.Value);}
                    }
                }
                }
            }
        }
        private async Task<int> ExecuteAsync(string executable, string arguments, bool elevated,bool sensors=false)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            installing = true; cancel.Enabled = false; progress.Style = ProgressBarStyle.Marquee;
            status.Text = sensors?"RMPCS is installed. Review the optional sensor window, complete or skip setup, then close that window to return here.":"Follow the " + (Program.Installer || elevated ? "Windows/Microsoft installer" : "RMPCS application") + " window. Waiting for it to finish...";
            operationHint.Text=sensors?"This optional step has its own review and vendor setup. Cancelling it does not roll back RMPCS. Nothing enables monitoring automatically.":Program.Installer||elevated?"This window remains open. Windows Installer displays detailed progress in its own window. To cancel, use that window and allow any rollback to finish.":"RMPCS is running. Close the application when you are finished; the portable launcher will keep its verified payload protected until then.";
            var start = new ProcessStartInfo(executable, arguments) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable) };
            if (elevated) start.Verb = "runas";
            try { using (var process = Process.Start(start)) { if (process == null) throw new IOException("The process did not start."); await Task.Run((Action)process.WaitForExit); return process.ExitCode; } }
            finally { installing = false; cancel.Enabled = true; }
        }
        private static string PrivateDirectory()
        {
            string root = Path.GetFullPath(Path.GetTempPath());
            for (string current = root; current != null; current = Path.GetDirectoryName(PathCompat.TrimEndingDirectorySeparatorCompat(current))) if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Temporary path contains a folder link; choose a normal Windows temp location.");
            string path = Path.Combine(root, "RMPCS-setup-" + Guid.NewGuid().ToString("N"));
            var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WindowsIdentity.GetCurrent().User, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) }) security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.CreateDirectory(path, security); return path;
        }
    }
    internal static class PathCompat
    { internal static string TrimEndingDirectorySeparatorCompat(string path) { string root = Path.GetPathRoot(path); return path.Length > root.Length ? path.TrimEnd('\\', '/') : path; } }
    internal static class Signature
    {
        public static void VerifyMicrosoft(string path, IntPtr handle)
        {
            var file = new TrustFile { Size = (uint)Marshal.SizeOf(typeof(TrustFile)), Path = path, Handle = handle };
            IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TrustFile))); Marshal.StructureToPtr(file, pointer, false);
            var trust = new TrustData { Size=(uint)Marshal.SizeOf(typeof(TrustData)), Ui=2, Revocation=1, Union=1, File=pointer, StateAction=1, Flags=0x80 };
            Guid action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
            try
            {
                int result = WinVerifyTrust(new IntPtr(-1), ref action, ref trust);
                if (result != 0) throw new InvalidDataException("Microsoft installer signature/trust check failed (0x" + result.ToString("X8") + "). No installer was executed.");
                using (var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path))) if (cert.GetNameInfo(X509NameType.SimpleName, false) != "Microsoft Corporation") throw new InvalidDataException("The installer is not signed by Microsoft Corporation.");
            }
            finally { trust.StateAction=2; WinVerifyTrust(new IntPtr(-1), ref action, ref trust); Marshal.DestroyStructure(pointer, typeof(TrustFile)); Marshal.FreeHGlobal(pointer); }
        }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle, Subject; }
        [StructLayout(LayoutKind.Sequential)] private struct TrustData { public uint Size; public IntPtr Policy, Sip; public uint Ui, Revocation, Union; public IntPtr File; public uint StateAction; public IntPtr State, Url; public uint Flags, Context; public IntPtr Settings; }
        [DllImport("wintrust.dll", ExactSpelling=true)] private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    }
}
