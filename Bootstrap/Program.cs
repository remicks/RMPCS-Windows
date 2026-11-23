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

[assembly: AssemblyVersion("0.9.1.0")]
[assembly: AssemblyFileVersion("0.9.1.0")]
[assembly: AssemblyProduct("RMPCS")]
[assembly: AssemblyCompany("Degenerates Inc.")]

namespace RMPCS.Bootstrap
{
    internal static class Program
    {
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
        { using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PayloadManifest")) using (var reader = new StreamReader(stream)) return BootstrapPolicy.ParseManifest(reader.ReadToEnd()); }
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
            bool preview = args.Length == 2 && (args[0] == "--ui-preview" || args[0] == "--ui-preview-options" || args[0] == "--ui-preview-finish");
            try
            {
                if (!Installer && !smoke && !preview && Array.IndexOf(args, "--setup") < 0 && RuntimePresent())
                {
                    using (var ownership = new HeldPayload(OpenVerifiedPayload()))
                    { using (var app = Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RMPCS.exe")) { UseShellExecute = false, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory })) app.WaitForExit(); }
                    return 0;
                }
                using (var form = new SetupForm())
                {
                    if(preview && args[0]=="--ui-preview-options")form.PreviewPage(1);
                    if(preview && args[0]=="--ui-preview-finish")form.PreviewPage(2);
                    if (smoke || preview) form.Shown += delegate { form.BeginInvoke((Action)delegate {
                        if (!smoke) using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0,0,form.Width,form.Height)); bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png); }
                        form.Close();
                    }); };
                    Application.Run(form);
                }
                return 0;
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
            for(int mask=0;mask<8;mask++)
            {
                string arguments=SetupChoices.MsiArguments(@"C:\safe path\RMPCS.msi",(mask&1)!=0,(mask&2)!=0,(mask&4)!=0);
                check(arguments.Contains("CREATE_DESKTOP="+((mask&1)!=0?"1":"0")) && arguments.Contains("CREATE_STARTMENU="+((mask&2)!=0?"1":"0")) && arguments.Contains("ALWAYS_ADMIN="+((mask&4)!=0?"1":"0")) && !arguments.Contains("RUN_AFTER"));
            }
            bool badPath=false;try{SetupChoices.MsiArguments("bad\"path",true,true,true);}catch(ArgumentException){badPath=true;}check(badPath);
            bool rejected = false; try { BootstrapPolicy.ParseManifest("../bad.exe|" + new string('a', 64)); } catch (InvalidDataException) { rejected = true; } check(rejected);
            using (var bytes = new MemoryStream(new byte[] {1,2,3})) check(BootstrapPolicy.Hash(bytes, true) != BootstrapPolicy.RuntimeSha512);
            bool signatureRejected = false;
            string self = Assembly.GetExecutingAssembly().Location;
            using (var heldSelf = new FileStream(self, FileMode.Open, FileAccess.Read, FileShare.Read))
                try { Signature.VerifyMicrosoft(self, heldSelf.SafeFileHandle.DangerousGetHandle()); } catch (InvalidDataException) { signatureRejected = true; }
            check(signatureRejected);
            if (Installer)
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InstallerPayload")) check(BootstrapPolicy.Hash(stream, false) == Manifest()["RMPCS.msi"]);
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
        private bool busy, installing;
        private string offlinePath;
        private async Task ContinueAsync()
        {
            if (busy) return;
            if (!Program.RuntimePresent() && !prerequisite.Checked) { MessageBox.Show(this, "Install the Desktop Runtime manually using the Microsoft link, then try again. No changes were made."); return; }
            if (!Program.RuntimePresent() && MessageBox.Show(this, "Download/use the selected Microsoft .NET 10 Desktop Runtime installer and launch it? This installs a shared system component. Follow Microsoft's setup and license prompts. RMPCS will not request an automatic restart.", "Install prerequisite", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            busy = true; progress.Visible=true; ShowPage(0); back.Visible=false; proceed.Enabled = offline.Enabled = prerequisite.Enabled = false;
            string temp = null;
            try
            {
                temp = PrivateDirectory();
                if (!Program.RuntimePresent())
                {
                    string runtime = Path.Combine(temp, "MicrosoftDesktopRuntime.exe");
                    if (offlinePath == null) await DownloadAsync(runtime);
                    else { using (var source = new FileStream(offlinePath, FileMode.Open, FileAccess.Read, FileShare.Read)) { if (source.Length <= 0 || source.Length > 200L * 1024 * 1024) throw new IOException("Unexpected offline runtime installer size."); using (var dest = new FileStream(runtime, FileMode.CreateNew, FileAccess.Write, FileShare.None)) await source.CopyToAsync(dest); } }
                    using (var held = new FileStream(runtime, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        status.Text = "Verifying runtime checksum and Microsoft Authenticode trust...";
                        if (BootstrapPolicy.Hash(held, true) != BootstrapPolicy.RuntimeSha512) throw new InvalidDataException("Runtime checksum mismatch. Nothing was executed. Choose Microsoft's exact 10.0.15 Windows x64 Desktop Runtime installer or retry the official download.");
                        Signature.VerifyMicrosoft(runtime, held.SafeFileHandle.DangerousGetHandle());
                        int code = await ExecuteAsync(runtime, "/install /norestart", true);
                        if (!BootstrapPolicy.AcceptedExit(code)) throw new InvalidOperationException(code == 1602 ? "Runtime installation was cancelled." : "Runtime installer returned " + code + ". Setup stopped.");
                        if (code == 3010) MessageBox.Show(this, "Microsoft reports that a restart is required. Save your work and restart later. RMPCS will not schedule a restart.", "Restart needed");
                    }
                    if (!Program.RuntimePresent()) throw new InvalidOperationException("The required runtime is not available yet. Restart if Microsoft requested it, then retry. A custom DOTNET_ROOT override can also prevent portable runtime discovery; installing a system runtime does not change that override.");
                }
                if (Program.Installer)
                {
                    string msi = Path.Combine(temp, "RMPCS.msi");
                    using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("InstallerPayload")) using (var dest = new FileStream(msi, FileMode.CreateNew, FileAccess.Write, FileShare.None)) await source.CopyToAsync(dest);
                    using (var held = new FileStream(msi, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (BootstrapPolicy.Hash(held, false) != Program.Manifest()["RMPCS.msi"]) throw new InvalidDataException("Embedded installer integrity check failed.");
                        int code = await ExecuteAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe"), SetupChoices.MsiArguments(msi,desktopChoice.Checked,menuChoice.Checked,adminChoice.Checked), false);
                        if (!BootstrapPolicy.AcceptedExit(code)) throw new InvalidOperationException(code == 1602 ? "RMPCS installation was cancelled." : "Windows Installer returned " + code + ". Review its message and retry.");
                        CompleteSetup(code);
                    }
                }
                else
                {
                    using (var held = new Program.HeldPayload(Program.OpenVerifiedPayload())) await ExecuteAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RMPCS.exe"), "", false);
                    status.Text = "RMPCS session finished.";
                }
            }
            catch (OperationCanceledException) { status.Text = "Download cancelled or timed out. No downloaded installer was executed. Close and reopen the launcher to retry."; }
            catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Setup did not complete", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally
            {
                installing = busy = false; progress.Visible=false; cancel.Enabled = true; cancel.Text="Close";
                proceed.Enabled = !cancellation.IsCancellationRequested; offline.Enabled = !Program.RuntimePresent() && !cancellation.IsCancellationRequested;
                prerequisite.Enabled = !Program.RuntimePresent(); progress.Style = ProgressBarStyle.Blocks;
                if (temp != null) { foreach (string name in new[] { "MicrosoftDesktopRuntime.exe", "RMPCS.msi" }) try { File.Delete(Path.Combine(temp, name)); } catch (IOException) { } catch (UnauthorizedAccessException) { } try { Directory.Delete(temp, false); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
        private async Task DownloadAsync(string path)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
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
                    }
                }
                }
            }
        }
        private async Task<int> ExecuteAsync(string executable, string arguments, bool elevated)
        {
            installing = true; cancel.Enabled = false; progress.Style = ProgressBarStyle.Marquee;
            status.Text = "Follow the " + (Program.Installer || elevated ? "Windows/Microsoft installer" : "RMPCS application") + " window. Waiting for it to finish...";
            var start = new ProcessStartInfo(executable, arguments) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable) };
            if (elevated) start.Verb = "runas";
            try { using (var process = Process.Start(start)) { if (process == null) throw new IOException("The process did not start."); await Task.Run((Action)process.WaitForExit); return process.ExitCode; } }
            finally { installing = false; cancel.Enabled = true; progress.Style = ProgressBarStyle.Blocks; }
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
