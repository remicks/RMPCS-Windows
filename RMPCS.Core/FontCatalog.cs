namespace RMPCS.Core;

public static class FontCatalog
{
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[] { "Barlow", "Rajdhani", "Atkinson Hyperlegible", "Lato", "Poppins", "Hind", "PT Sans", "Oxygen", "Windows system" });
    public static IReadOnlyList<string> LicenseIds { get; } = Array.AsReadOnly(new[] { "barlow", "rajdhani", "atkinsonhyperlegible", "lato", "poppins", "hind", "ptsans", "oxygen" });
    public static string Normalize(string? value) => Names.Contains(value, StringComparer.Ordinal) ? value! : "Barlow";
}
