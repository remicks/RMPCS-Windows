using System.Text.Json;
namespace RMPCS.Core;
public sealed record WidgetPlacement(string Key, string Monitor, double X, double Y, bool Locked);
public static class WidgetLayout
{
    public static readonly string[] Metrics = ["CPU", "RAM", "Download", "Upload", "Drive", "Uptime"];
    public static bool ValidKey(string? key) => key is not null && (Metrics.Contains(key) || key.StartsWith("Temp:", StringComparison.Ordinal) && key.Length == 69 && key[5..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    public static WidgetPlacement[] Parse(string? value)
    {
        if (value is null || value.Length > 4096) return [];
        try { return (JsonSerializer.Deserialize<WidgetPlacement[]>(value, new JsonSerializerOptions { MaxDepth = 3 }) ?? []).Where(p => p is not null && ValidKey(p.Key) && SidebarLayout.ValidMonitor(p.Monitor) && double.IsFinite(p.X) && double.IsFinite(p.Y)).DistinctBy(p => p.Key).Take(14).Select(p => p with { X = Math.Clamp(p.X, 0, 1), Y = Math.Clamp(p.Y, 0, 1) }).OrderBy(p => p.Key, StringComparer.Ordinal).ToArray(); }
        catch (JsonException) { return []; }
    }
    public static string Save(IEnumerable<WidgetPlacement> values) => JsonSerializer.Serialize(Parse(JsonSerializer.Serialize(values.Where(p=>p is not null && double.IsFinite(p.X) && double.IsFinite(p.Y)).Take(14))));
}
