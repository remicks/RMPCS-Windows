namespace RMPCS.Core;

public static class HealthRules
{
    public static string StorageAdvice(long free, long total)
    {
        if (total <= 0 || free < 0) return "Storage usage unavailable.";
        double percent = 100d * free / total;
        return percent < 10 || free < 10L * 1024 * 1024 * 1024
            ? "Low free space: review a cleanup preview and large personal files."
            : "No low-space warning at this snapshot.";
    }
    public static string MemoryAdvice(double usedPercent) => usedPercent >= 85
        ? "High memory use: review open apps before starting repairs."
        : "Memory use is below the snapshot warning threshold.";
    public static string CpuAdvice(double usedPercent) => usedPercent >= 85
        ? "CPU use is high during this short sample. Consider deferring maintenance."
        : "CPU use is below the snapshot warning threshold.";
}

public sealed record StartupItem(string Id, string Name, string Command, string Scope, bool Enabled, bool Expand, string OwnerSid)
{
    [System.Text.Json.Serialization.JsonIgnore] public string ScopeLabel => Scope switch { "User64" => "This Windows user", "Machine32" => "All users (32-bit)", _ => "All users (64-bit)" };
    [System.Text.Json.Serialization.JsonIgnore] public string SwitchLabel => Enabled ? "On" : "Off";
    [System.Text.Json.Serialization.JsonIgnore] public string AccessibleName => Name + " startup entry: " + SwitchLabel + ", " + ScopeLabel;
    public string State => Enabled ? "Run entry present" : "Disabled by RMPCS";
}
public static class StartupPolicy
{
    public static bool ValidScope(string scope) => scope is "User64" or "Machine64" or "Machine32";
    public static string Id(string scope, string name, string sid)
    {
        if (!ValidScope(scope) || string.IsNullOrEmpty(name)) throw new ArgumentException("Unsupported startup entry.");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope + "\n" + sid + "\n" + name)));
    }
    public static void Validate(StartupItem item, string currentSid)
    {
        if (!ValidScope(item.Scope) || item.Name.Length == 0 || item.Command.Length > 32767 || item.Id != Id(item.Scope, item.Name, item.OwnerSid)) throw new ArgumentException("Invalid startup backup.");
        if (item.Scope == "User64" && item.OwnerSid != currentSid) throw new ArgumentException("This startup entry belongs to a different Windows user.");
        if (item.Scope != "User64" && item.OwnerSid != "machine") throw new ArgumentException("Invalid machine startup identity.");
    }
}
