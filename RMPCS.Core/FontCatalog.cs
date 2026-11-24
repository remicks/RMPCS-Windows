namespace RMPCS.Core;

public static class FontCatalog
{
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[] { "Barlow", "Rajdhani", "Atkinson Hyperlegible", "Windows system" });
    public static string Normalize(string? value) => Names.Contains(value, StringComparer.Ordinal) ? value! : "Barlow";
}
