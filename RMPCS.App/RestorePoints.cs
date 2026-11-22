using System.Runtime.InteropServices;
using System.Security.Principal;
namespace RMPCS.App;

internal sealed record RestorePointResult(bool Verified, string Message);
internal static class RestorePoints
{
    public static Task<RestorePointResult> CreateAsync() => Task.Run(() =>
    {
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return new RestorePointResult(false, "Administrator rights are required.");
        object? locator = null, service = null, definition = null, methods = null, method = null, inputDefinition = null, input = null, output = null;
        string description = "RMPCS " + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + " " + Guid.NewGuid().ToString("N")[..8];
        try
        {
            locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator", true)!);
            service = ((dynamic)locator!).ConnectServer(".", @"root\default");
            long previous = MaximumSequence(service, null);
            definition = ((dynamic)service).Get("SystemRestore"); methods = ((dynamic)definition).Methods_;
            method = ((dynamic)methods).Item("CreateRestorePoint"); inputDefinition = ((dynamic)method).InParameters;
            input = ((dynamic)inputDefinition).SpawnInstance_();
            Set(input, "Description", description); Set(input, "RestorePointType", 12); Set(input, "EventType", 100);
            output = ((dynamic)service).ExecMethod("SystemRestore", "CreateRestorePoint", input);
            uint status = Convert.ToUInt32(Get(output, "ReturnValue"));
            if (status != 0) return new RestorePointResult(false, "Windows did not create a restore point (status " + status + "). System Protection may be unavailable or disabled.");
            long verified = MaximumSequence(service, description);
            return RMPCS.Core.RestoreVerification.IsNew(status, previous, verified) ? new RestorePointResult(true, "A new restore point was verified. Sequence " + verified + ". System Restore is not a backup of deleted personal files.") : new RestorePointResult(false, "Windows accepted the request, but no new matching restore point was verified. Creation may be frequency-limited or System Protection unavailable. No frequency, service or protection setting was changed.");
        }
        catch { return new RestorePointResult(false, "Restore-point creation/verification was unavailable. Check Windows System Protection. RMPCS did not enable services or change restore-point frequency settings."); }
        finally { foreach (var item in new[] { output, input, inputDefinition, method, methods, definition, service, locator }) Release(item); }
    });
    private static object? Get(object obj, string name)
    {
        object? properties = null, property = null;
        try { properties = ((dynamic)obj).Properties_; property = ((dynamic)properties).Item(name); return ((dynamic)property).Value; }
        finally { Release(property); Release(properties); }
    }
    private static void Set(object obj, string name, object value)
    {
        object? properties = null, property = null;
        try { properties = ((dynamic)obj).Properties_; property = ((dynamic)properties).Item(name); ((dynamic)property).Value = value; }
        finally { Release(property); Release(properties); }
    }
    private static long MaximumSequence(object service, string? description)
    {
        object? results = null; long maximum = -1;
        try
        {
            results = ((dynamic)service).ExecQuery("SELECT SequenceNumber, Description FROM SystemRestore"); int count = 0;
            foreach (object row in (dynamic)results)
            {
                try { if (++count > 2048) throw new InvalidOperationException("Restore inventory limit reached."); if (description is null || string.Equals(Convert.ToString(Get(row,"Description")), description, StringComparison.Ordinal)) maximum = Math.Max(maximum, Convert.ToInt64(Get(row,"SequenceNumber"))); }
                finally { Release(row); }
            }
            return maximum;
        }
        finally { Release(results); }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) try { Marshal.FinalReleaseComObject(value); } catch { } }
}
