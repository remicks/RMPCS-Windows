using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.IO;
using RMPCS.Core;

namespace RMPCS.App;

// Optional, user-enabled, hash-pinned LibreHardwareMonitor backend. No driver installation,
// fan/voltage controls or unrestricted library search is performed by this reader.
internal static class OnboardSensors
{
    private static volatile object? computer;
    private static SensorContext? context;
    private static int closeRequested;
    internal static bool IsOpen => computer is not null;
    internal static void RequestClose() => Volatile.Write(ref closeRequested,1);
    internal static void CloseIfRequested()
    {
        if(Interlocked.Exchange(ref closeRequested,0)==0) return;
        var old=computer; computer=null;
        if(old is not null) old.GetType().GetMethod("Close")!.Invoke(old,null);
    }
    internal static IReadOnlyList<TemperatureValue> Read()
    {
        if(!new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) || !SensorComponents.DriverReady()) throw new InvalidOperationException("In-app sensors require an administrator session and PawnIO 2.2.0 or newer. No driver was installed.");
        if(computer is null)
        {
            context ??= new();
            using var verified=SensorComponents.OpenLibrary("LibreHardwareMonitorLib.dll");
            var assembly=context.Assemblies.FirstOrDefault(a=>a.GetName().Name=="LibreHardwareMonitorLib") ?? context.LoadFromStream(verified);
            var instance=Activator.CreateInstance(assembly.GetType("LibreHardwareMonitor.Hardware.Computer",true)!)!;
            try
            {
                foreach(string property in new[]{"IsCpuEnabled","IsGpuEnabled","IsMotherboardEnabled"}) instance.GetType().GetProperty(property)!.SetValue(instance,true);
                // Storage uses Windows counters; memory/controllers/network/PSU/battery stay disabled.
                instance.GetType().GetMethod("Open")!.Invoke(instance,null); computer=instance;
            }
            catch { instance.GetType().GetMethod("Close")!.Invoke(instance,null); throw; }
        }
        var rows=new List<TemperatureValue>(); var visited=new HashSet<string>(); int hardwareCount=0,sensorCount=0;
        Visit(Get(computer,"Hardware") as IEnumerable,0);
        return rows;
        void Visit(IEnumerable? hardware,int depth)
        {
            if(hardware is null || depth>8) return;
            foreach(object item in hardware)
            {
                if(++hardwareCount>64 || rows.Count>=128) return;
                string id=Convert.ToString(Get(item,"Identifier")) ?? ""; if(!visited.Add(id)) continue;
                item.GetType().GetMethod("Update")!.Invoke(item,null);
                string category=TemperaturePolicy.Category(Convert.ToString(Get(item,"HardwareType")) ?? "");
                if(Get(item,"Sensors") is IEnumerable sensors) foreach(object sensor in sensors)
                {
                    if(++sensorCount>1024 || rows.Count>=128) break;
                    if(Convert.ToString(Get(sensor,"SensorType"))!="Temperature") continue;
                    string sensorId=Convert.ToString(Get(sensor,"Identifier")) ?? "";
                    rows.Add(new(new("In-app hardware",category,TemperaturePolicy.Key("In-app hardware",sensorId),TemperaturePolicy.Clean(Convert.ToString(Get(item,"Name"))+" / "+Convert.ToString(Get(sensor,"Name")))),TemperaturePolicy.Celsius("In-app hardware",Get(sensor,"Value"))));
                }
                Visit(Get(item,"SubHardware") as IEnumerable,depth+1);
            }
        }
    }
    private static object? Get(object value,string property) => value.GetType().GetProperty(property)!.GetValue(value);
    private sealed class SensorContext : AssemblyLoadContext
    {
        internal SensorContext():base("RMPCS reviewed sensors",false) { }
        protected override Assembly? Load(AssemblyName name)
        {
            string file=(name.Name ?? "")+".dll";
            if(SensorComponents.Files.ContainsKey(file)) { using var stream=SensorComponents.OpenLibrary(file); return LoadFromStream(stream); }
            if(name.Name is "System" or "netstandard" or "mscorlib" || name.Name?.StartsWith("System.",StringComparison.Ordinal)==true || name.Name?.StartsWith("Microsoft.Win32.",StringComparison.Ordinal)==true) return null;
            throw new FileNotFoundException("Unreviewed sensor assembly dependency blocked: "+name.Name);
        }
        protected override IntPtr LoadUnmanagedDll(string name)
        {
            if(name.Length>80 || name.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-'))) throw new DllNotFoundException("Untrusted native sensor dependency.");
            return NativeLibrary.Load(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)?name:name+".dll"));
        }
    }
}
