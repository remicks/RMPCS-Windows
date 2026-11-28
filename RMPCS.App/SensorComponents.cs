using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Microsoft.Win32;

namespace RMPCS.App;

internal static class SensorComponents
{
    internal sealed record SetupOutcome(bool Ready,bool RestartRequired,string Message);
    internal const string DriverUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";
    internal const string DriverHash = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";
    internal const string DriverSigner = "F380DCC9F706E2756A5047B832FFE719E1BC35F5";
    private const string LibraryUrl = "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/download/v0.9.6/LibreHardwareMonitor.NET.10.zip";
    private const string LibraryHash = "29739C4959B01B348FDDAD87664066634BCFD4F46E9BF41E4E916C318BCFDB99";
    internal static readonly IReadOnlyDictionary<string,(string Entry,string Hash)> Files = new Dictionary<string,(string,string)>(StringComparer.OrdinalIgnoreCase) {
        ["LibreHardwareMonitorLib.dll"] = ("LibreHardwareMonitorLib.dll","984B539048880D37257A5EDEAB36378152C54943069F400D68A34565292D35DA"),
        ["DiskInfoToolkit.dll"] = ("DiskInfoToolkit.dll","F3E9030740E6351FD3586D38B72BDEB21D530CD7CCDE5D18743C25D28584226D"),
        ["HidSharp.dll"] = ("HidSharp.dll","79F2BC8DDFF102E6DBEDE84B2C211A3AEA2ED675BF762FA0F02ECB65F61EE812"),
        ["BlackSharp.Core.dll"] = ("BlackSharp.Core.dll","FBACD3AE711BE82F35EDBF0DD1DA00D01CC2629E19D3CBF24A1B5EADCD9D4872"),
        ["RAMSPDToolkit-NDD.dll"] = ("RAMSPDToolkit-NDD.dll","42C1CCAF90592542EC64E801AE599ED803BB98F4039B2ABB7119CB8EA2087CBA"),
        ["System.Management.dll"] = ("runtimes/win/lib/net10.0/System.Management.dll","01F9360D110863F810431C4D29ADA0FCA89F267343D030E98AA823EA4C0C0EBB"),
        ["System.IO.Ports.dll"] = ("runtimes/win/lib/net10.0/System.IO.Ports.dll","331FF3528809BF54382461FDF0FBDFDCB6C221D770DA7C755EF531801A80554A"),
        ["Mono.Posix.NETStandard.dll"] = ("runtimes/win-x64/lib/netstandard2.0/Mono.Posix.NETStandard.dll","BE0215D90462ADBAE5F03D6E5C3A969C9842A866B013DF7A395769C5C352FA7E") };
    internal static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RMPCS-SensorComponents");
    internal static string LibraryRoot => Path.Combine(Root, "LHM-0.9.6");
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid,null), SystemSid = new(WellKnownSidType.LocalSystemSid,null), Readers = new(WellKnownSidType.AuthenticatedUserSid,null);
    internal static bool DriverReady()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
        return Version.TryParse(key?.GetValue("DisplayVersion") as string,out var version) && version >= new Version(2,2,0);
    }
    internal static void AssertPath(string path)
    {
        for (string? item = Path.GetFullPath(path); item is not null; item = Path.GetDirectoryName(item))
            if ((Directory.Exists(item) || File.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked sensor component paths are not permitted.");
    }
    private static void ProtectDirectory(string path)
    {
        AssertPath(path);if(Directory.Exists(path)){AssertProtected(path);return;}
        var security = new DirectorySecurity(); security.SetAccessRuleProtection(true,false); security.SetOwner(Administrators);
        foreach (var sid in new[] { Administrators, SystemSid }) security.AddAccessRule(new(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        security.AddAccessRule(new(Readers,FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        new DirectoryInfo(path).Create(security); AssertProtected(path);
    }
    private static FileStream CreateProtectedFile(string path)
    {
        var security = new FileSecurity(); security.SetAccessRuleProtection(true,false); security.SetOwner(Administrators);
        foreach (var sid in new[] { Administrators, SystemSid }) security.AddAccessRule(new(sid,FileSystemRights.FullControl,AccessControlType.Allow));
        security.AddAccessRule(new(Readers,FileSystemRights.ReadAndExecute,AccessControlType.Allow)); return new FileInfo(path).Create(FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.None,security);
    }
    internal static void AssertProtected(string path)
    {
        AssertPath(path);
        FileSystemSecurity security = Directory.Exists(path) ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
        bool Trusted(IdentityReference? sid)=>sid is not null && (sid.Equals(Administrators)||sid.Equals(SystemSid)||sid.Value=="S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");
        if (!Trusted(security.GetOwner(typeof(SecurityIdentifier)))) throw new IOException("Sensor component owner is not protected.");
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true,true,typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && (rule.PropagationFlags&PropagationFlags.InheritOnly)==0 && (rule.FileSystemRights & (FileSystemRights.Write|FileSystemRights.Delete|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership)) != 0 && !Trusted(rule.IdentityReference)) throw new IOException("Sensor component location permits non-administrator changes.");
    }
    internal static FileStream OpenLibrary(string name)
    {
        if (!Files.TryGetValue(name,out var entry)) throw new IOException("Unapproved sensor assembly.");
        AssertProtected(Root); AssertProtected(LibraryRoot); string path = Path.Combine(LibraryRoot,name); AssertProtected(path);
        var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        try { CheckHash(stream,entry.Hash); stream.Position=0; return stream; } catch { stream.Dispose(); throw; }
    }
    internal static void CheckHash(Stream stream,string expected)
    {
        if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.Ordinal)) throw new IOException("Sensor component integrity check failed. Nothing was executed.");
    }
    private static void StageLibrary(string path,MemoryStream data,string hash)
    {
        if(File.Exists(path))return;
        string pending=path+".new-"+Guid.NewGuid().ToString("N");
        try
        {
            using(var output=CreateProtectedFile(pending)){data.Position=0;data.CopyTo(output);output.Flush(true);}
            AssertProtected(pending);using(var verified=new FileStream(pending,FileMode.Open,FileAccess.Read,FileShare.Read))CheckHash(verified,hash);
            File.Move(pending,path,false); // Atomic publish; interrupted writes never become final library files.
        }
        finally{try{File.Delete(pending);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
    private static async Task<byte[]> Download(string url,string hash,int max)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var handler = new HttpClientHandler { AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false }; using var client = new HttpClient(handler) { Timeout=TimeSpan.FromSeconds(90) };
        Uri address = new(url);
        for(int redirect=0;redirect<4;redirect++)
        {
            if(address.Scheme!="https" || address.Port!=443 || address.UserInfo.Length!=0 || address.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com")) throw new IOException("Untrusted sensor download redirect.");
            using var response=await client.GetAsync(address,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308) { address=new Uri(address,response.Headers.Location ?? throw new IOException("Missing redirect.")); continue; }
            response.EnsureSuccessStatusCode(); if(response.Content.Headers.ContentLength>max) throw new IOException("Sensor download too large.");
            await using var input=await response.Content.ReadAsStreamAsync(deadline.Token); using var output=new MemoryStream(); byte[] buffer=new byte[16384]; int count;
            while((count=await input.ReadAsync(buffer,deadline.Token))>0) { if(output.Length+count>max) throw new IOException("Sensor download exceeds size limit."); output.Write(buffer,0,count); }
            output.Position=0; CheckHash(output,hash); return output.ToArray();
        }
        throw new IOException("Too many sensor download redirects.");
    }
    internal static async Task<SetupOutcome> InstallAsync(Action<string> report)
    {
        if(RuntimeInformation.OSArchitecture!=Architecture.X64 || RuntimeInformation.ProcessArchitecture!=Architecture.X64 || Environment.OSVersion.Version.Build<22000 || !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new UnauthorizedAccessException("Use an administrator session on Windows 11 x64.");
        AssertProtected(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));ProtectDirectory(Root);
        string gate=Path.Combine(Root,"setup.lock");if(!File.Exists(gate)){using var created=CreateProtectedFile(gate);}AssertProtected(gate);
        using var lease=new FileStream(gate,FileMode.Open,FileAccess.ReadWrite,FileShare.None); // Across installer and Settings processes; never force another setup to stop.
        report("Downloading pinned official sensor library and PawnIO 2.2.0 installer. No driver has run.");
        byte[] library=await Download(LibraryUrl,LibraryHash,12_000_000), driver=await Download(DriverUrl,DriverHash,4_000_000);
        AssertProtected(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));ProtectDirectory(Root); ProtectDirectory(LibraryRoot);
        using(var zip=new ZipArchive(new MemoryStream(library),ZipArchiveMode.Read))
            foreach(var (name,entry) in Files)
            {
                var matches=zip.Entries.Where(e=>e.FullName==entry.Entry).ToArray(); if(matches.Length!=1 || matches[0].Length>2_000_000) throw new IOException("Invalid sensor archive entry.");
                using var input=matches[0].Open(); using var data=new MemoryStream(); input.CopyTo(data); data.Position=0; CheckHash(data,entry.Hash);
                string path=Path.Combine(LibraryRoot,name);
                StageLibrary(path,data,entry.Hash);
                using var verified=OpenLibrary(name);
            }
        if(DriverReady())return new(true,false,"Existing PawnIO 2.2.0+ retained; libraries verified. Enable in-app reads, discover/select sources and Apply in Settings. No driver setup was run. Sensor availability is hardware-dependent.");
        string staging=Path.Combine(Root,"Setup-"+Guid.NewGuid().ToString("N")); ProtectDirectory(staging); string setup=Path.Combine(staging,"PawnIO_setup.exe");
        try
        {
            using(var file=CreateProtectedFile(setup)) await file.WriteAsync(driver);
            using var held=new FileStream(setup,FileMode.Open,FileAccess.Read,FileShare.Read); CheckHash(held,DriverHash); held.Position=0;
            await Task.Run(()=>VerifyDriverSignature(setup,held.SafeFileHandle.DangerousGetHandle()));
            report("Hash, protected staging, Authenticode trust and pinned namazso.eu signer verified. Complete the visible official setup; Windows security settings are unchanged.");
            using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(setup) { UseShellExecute=true,Verb="runas",WorkingDirectory=staging }) ?? throw new IOException("Driver setup did not start.");
            await process.WaitForExitAsync();
            bool restart=process.ExitCode==3010,ready=(process.ExitCode is 0 or 3010) && DriverReady();
            return new(ready,restart,restart?"The vendor reports a restart is required. Restart Windows before enabling in-app reads. No monitoring was enabled automatically.":ready?"PawnIO registration and verified libraries are present. Restart if the vendor requested it, then enable in-app reads, discover/select sensors and Apply in Settings. No monitoring was enabled automatically.":$"Vendor setup closed (exit {process.ExitCode}) without confirmed completion. RMPCS remains installed. Restart if requested or retry from Settings; no monitoring was enabled.");
        }
        finally { try { File.Delete(setup); Directory.Delete(staging,false); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
    }
    internal static void VerifyDriverSignature(string path,IntPtr handle)
    {
        var file=new TrustFile { Size=(uint)Marshal.SizeOf<TrustFile>(),Path=path,Handle=handle }; IntPtr pointer=Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>()); Marshal.StructureToPtr(file,pointer,false);
        var trust=new TrustData { Size=(uint)Marshal.SizeOf<TrustData>(),Ui=2,Revocation=1,Union=1,File=pointer,StateAction=1,Flags=0x80 }; Guid action=new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try
        {
            if(WinVerifyTrust(new(-1),ref action,ref trust)!=0) throw new IOException("Driver installer trust/revocation verification failed; setup was not run.");
            IntPtr data=WTHelperProvDataFromStateData(trust.State),signer=WTHelperGetProvSignerFromChain(data,0,false,0);
            if(data==IntPtr.Zero || signer==IntPtr.Zero)throw new IOException("Missing verified signer chain.");
            var chain=Marshal.PtrToStructure<SignerHeader>(signer);if(chain.Count==0 || chain.Count>64 || chain.Certificates==IntPtr.Zero)throw new IOException("Invalid signer chain.");
            var leaf=Marshal.PtrToStructure<ProviderCertificate>(chain.Certificates);if(leaf.Certificate==IntPtr.Zero)throw new IOException("Missing signer certificate.");
            var encoded=Marshal.PtrToStructure<CertificateContext>(leaf.Certificate);if(encoded.Length==0 || encoded.Length>1_000_000 || encoded.Bytes==IntPtr.Zero)throw new IOException("Invalid signer certificate.");
            byte[] bytes=new byte[encoded.Length];Marshal.Copy(encoded.Bytes,bytes,0,bytes.Length);using var cert=X509CertificateLoader.LoadCertificate(bytes);
            if(cert.Thumbprint!=DriverSigner || cert.GetNameInfo(X509NameType.SimpleName,false)!="namazso.eu") throw new IOException("Unexpected driver installer publisher; setup was not run.");
        }
        finally { trust.StateAction=2; WinVerifyTrust(new(-1),ref action,ref trust); Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer); }
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle,Subject; }
    [StructLayout(LayoutKind.Sequential)] private struct FileTime {public uint Low,High;}
    [StructLayout(LayoutKind.Sequential)] private struct SignerHeader {public uint Size;public FileTime VerifiedAt;public uint Count;public IntPtr Certificates;}
    [StructLayout(LayoutKind.Sequential)] private struct ProviderCertificate {public uint Size;public IntPtr Certificate;}
    [StructLayout(LayoutKind.Sequential)] private struct CertificateContext {public uint Encoding;public IntPtr Bytes;public uint Length;public IntPtr Info,Store;}
    [StructLayout(LayoutKind.Sequential)] private struct TrustData { public uint Size; public IntPtr Policy,Sip; public uint Ui,Revocation,Union; public IntPtr File; public uint StateAction; public IntPtr State,Url; public uint Flags,Context; public IntPtr Settings; }
    [DllImport("wintrust.dll",ExactSpelling=true)] private static extern int WinVerifyTrust(IntPtr hwnd,ref Guid action,ref TrustData data);
    [DllImport("wintrust.dll",ExactSpelling=true)] private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DllImport("wintrust.dll",ExactSpelling=true)] private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr data,uint index,[MarshalAs(UnmanagedType.Bool)]bool counter,uint counterIndex);
}
