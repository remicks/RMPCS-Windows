using System.Runtime.InteropServices;
using RMPCS.Core;
namespace RMPCS.App;

internal static class TemperatureProbe
{
    private static int active;
    internal static Task<IReadOnlyList<TemperatureValue>> QueryAsync(string provider, bool onboard)
    {
        if (!TemperaturePolicy.Providers.Contains(provider)) throw new ArgumentException("Unknown sensor provider.");
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0) throw new InvalidOperationException("A sensor query is still busy. No additional worker was started.");
        var task = Task.Run<IReadOnlyList<TemperatureValue>>(() => { try { return provider == "In-app hardware" ? onboard ? OnboardSensors.Read() : [] : ReadWmi(provider); } finally { try { OnboardSensors.CloseIfRequested(); } finally { Volatile.Write(ref active, 0); } } });
        // Timeout never releases the worker gate early: hung providers cannot accumulate threads.
        return task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    internal static void CloseOnboard()
    {
        if(!OnboardSensors.IsOpen && Volatile.Read(ref active)==0)return;
        OnboardSensors.RequestClose();
        if(Interlocked.CompareExchange(ref active,1,0)==0) _=Task.Run(()=>{try{OnboardSensors.CloseIfRequested();}catch{ }finally{Volatile.Write(ref active,0);}});
    }
    private static IReadOnlyList<TemperatureValue> ReadWmi(string provider)
    {
        string scope = provider switch { "Windows firmware" => @"root\WMI", "Windows storage" => @"root\Microsoft\Windows\Storage", "LibreHardwareMonitor WMI" => @"root\LibreHardwareMonitor", "OpenHardwareMonitor WMI" => @"root\OpenHardwareMonitor", _ => throw new ArgumentException("Unknown WMI provider.") };
        var values = new List<TemperatureValue>(); object? locator = null, service = null;
        try
        {
            locator = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("76A64158-CB41-11D1-8B02-00600806D9B6"), true)!);
            service = ((dynamic)locator!).ConnectServer(".", scope);
            if (provider == "Windows firmware")
                Rows(service, "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature", row => Add("Firmware", Text(row,"InstanceName"), "Thermal zone " + Text(row,"InstanceName"), Get(row,"CurrentTemperature")));
            else if (provider == "Windows storage")
                Rows(service, "SELECT DeviceId, Temperature FROM MSFT_StorageReliabilityCounter", row => Add("Storage", Text(row,"DeviceId"), "Drive " + Text(row,"DeviceId"), Get(row,"Temperature")));
            else
            {
                var hardware = new Dictionary<string, (string Type, string Parent)>(StringComparer.Ordinal);
                Rows(service, "SELECT Identifier, HardwareType, Parent FROM Hardware", row => hardware[Text(row,"Identifier")] = (Text(row,"HardwareType"), Text(row,"Parent")));
                Rows(service, "SELECT Identifier, Parent, Name, Value FROM Sensor WHERE SensorType = 'Temperature'", row =>
                {
                    string parent = Text(row,"Parent"), category = "Other";
                    for (int i = 0; i < 8 && hardware.TryGetValue(parent, out var node); i++) { category = TemperaturePolicy.Category(node.Type); if (category != "Other") break; parent = node.Parent; }
                    Add(category, Text(row,"Identifier"), Text(row,"Name"), Get(row,"Value"));
                });
            }
            return values;
        }
        finally { Release(service); Release(locator); }
        void Add(string category, string id, string name, object? value)
        {
            if (id.Length == 0 || values.Count >= 128) return;
            values.Add(new(new(provider, category, TemperaturePolicy.Key(provider,id), TemperaturePolicy.Clean(name)), TemperaturePolicy.Celsius(provider,value)));
        }
    }
    private static void Rows(object service, string query, Action<object> visit)
    {
        object? results = null; int count = 0;
        try { results = ((dynamic)service).ExecQuery(query, "WQL", 48); foreach (object row in (dynamic)results) { try { if (++count > 128) break; visit(row); } finally { Release(row); } } }
        finally { Release(results); }
    }
    private static object? Get(object row, string name)
    {
        object? properties = null, item = null;
        try { properties = ((dynamic)row).Properties_; item = ((dynamic)properties).Item(name); return ((dynamic)item).Value; }
        finally { Release(item); Release(properties); }
    }
    private static string Text(object row, string name) => TemperaturePolicy.Clean(Convert.ToString(Get(row,name)), 160);
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}

internal sealed class TemperaturePoller
{
    private int generation;
    private bool busy;
    private long lastAttempt;
    private Dictionary<string,(double? Value,long At)> values = new(StringComparer.Ordinal);
    internal void Reset() { generation++; values = new(StringComparer.Ordinal); lastAttempt = 0; TemperatureProbe.CloseOnboard(); }
    internal IReadOnlyDictionary<string,double?> Snapshot(TemperaturePreferences options)
    {
        if (!options.Enabled || !options.Views.Any()) return new Dictionary<string,double?>();
        if (!busy && (lastAttempt == 0 || System.Diagnostics.Stopwatch.GetElapsedTime(lastAttempt).TotalSeconds >= 5)) { lastAttempt = System.Diagnostics.Stopwatch.GetTimestamp(); _ = Refresh(options, generation); }
        return values.Where(v => System.Diagnostics.Stopwatch.GetElapsedTime(v.Value.At).TotalSeconds <= 15).ToDictionary(v => v.Key,v => v.Value.Value,StringComparer.Ordinal);
    }
    private async Task Refresh(TemperaturePreferences options, int ticket)
    {
        busy = true; var fresh = new Dictionary<string,(double? Value,long At)>(StringComparer.Ordinal); var selected = options.Selected();
        try
        {
            foreach (string provider in selected.Select(s => s.Provider).Distinct())
            {
                if(ticket != generation) break;
                long started=System.Diagnostics.Stopwatch.GetTimestamp();
                try { foreach (var row in await TemperatureProbe.QueryAsync(provider, options.Onboard)) if (selected.Any(s => s.Key == row.Source.Key && s.Category == row.Source.Category)) fresh[row.Source.Key] = (row.Celsius,started); }
                catch (Exception ex) when (ex is COMException or InvalidOperationException or TimeoutException or System.IO.IOException or System.Security.SecurityException or System.Reflection.TargetInvocationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or UnauthorizedAccessException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or TypeLoadException or MemberAccessException or NotSupportedException or System.ComponentModel.Win32Exception) { }
            }
            if (ticket == generation) values = fresh;
        }
        finally { busy = false; }
    }
}
