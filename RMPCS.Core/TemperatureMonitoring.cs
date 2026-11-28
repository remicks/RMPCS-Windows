using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RMPCS.Core;
public sealed record TemperatureSource(string Provider, string Category, string Key, string Name) { public string Display => $"{Category} · {Name} [{Provider}]"; }
public sealed record TemperatureValue(TemperatureSource Source, double? Celsius);
public sealed record TemperaturePreferences
{
    public bool Enabled { get; init; }
    public bool Onboard { get; init; }
    public string SelectionJson { get; init; } = "[]";
    public MetricViewPreferences Views { get; init; } = new();
    public TemperatureSource[] Selected() => TemperaturePolicy.Selections(SelectionJson);
    public TemperaturePreferences Normalize() => this with { SelectionJson = JsonSerializer.Serialize(Selected()), Views = Views ?? new() };
}
public static class TemperaturePolicy
{
    public static readonly string[] Providers = ["Windows firmware", "Windows storage", "LibreHardwareMonitor WMI", "OpenHardwareMonitor WMI", "In-app hardware"];
    public static readonly string[] Categories = ["CPU", "GPU", "Storage", "Motherboard", "Firmware", "Other"];
    public static string Clean(string? text, int limit = 64) => new((text ?? "").Where(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format).Take(limit).ToArray());
    public static string Key(string provider, string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(provider + "|" + id))).ToLowerInvariant();
    public static TemperatureSource[] Selections(string? json)
    {
        if (json is null || json.Length > 8192) return [];
        try { return (JsonSerializer.Deserialize<TemperatureSource[]>(json, new JsonSerializerOptions { MaxDepth = 4 }) ?? []).Where(s => s is not null && Providers.Contains(s.Provider) && Categories.Contains(s.Category) && s.Key is { Length: 64 } && s.Key.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')).DistinctBy(s => s.Key).Take(8).Select(s => s with { Name = Clean(s.Name,48) }).OrderBy(s => s.Key, StringComparer.Ordinal).ToArray(); }
        catch (JsonException) { return []; }
    }
    public static double? Celsius(string provider, object? value)
    {
        if (value is null) return null;
        try { double n = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture); if (provider == "Windows firmware") n = n / 10 - 273.15; if (Math.Abs(n)<.01) return null; return double.IsFinite(n) && n is >= -50 and <= 200 ? n : null; }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { return null; }
    }
    public static string Category(string hardware) => hardware switch { "CPU" or "Cpu" => "CPU", "GpuNvidia" or "GpuAmd" or "GpuAti" or "GpuIntel" => "GPU", "Storage" or "HDD" => "Storage", "Motherboard" or "Mainboard" or "SuperIO" or "EmbeddedController" => "Motherboard", _ => "Other" };
}
