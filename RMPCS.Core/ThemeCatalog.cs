using System.Globalization;
namespace RMPCS.Core;

public sealed record AccentPreset(string Name, string Hex);
public static class ThemeCatalog
{
    public static readonly string[] Modes = ["System", "Light", "Dark"];
    public static IReadOnlyList<AccentPreset> Presets { get; } = Array.AsReadOnly(new[] {
        new AccentPreset("Midnight Purple", "#D58AFF"), new("Electric Blue", "#629BFF"), new("Neon Cyan", "#45DDEB"), new("Emerald Green", "#64E6BA"),
        new("Crimson Red", "#BE2B4E"), new("Sunset Orange", "#FFB66B"), new("Hot Pink", "#F07DBB"), new("Gold", "#EAC75F") });
    public static string NormalizeHex(string? value)
    {
        string hex = (value ?? "").Trim(); if (!hex.StartsWith('#')) hex = "#" + hex;
        return hex.Length == 7 && hex[1..].All(char.IsAsciiHexDigit) ? hex.ToUpperInvariant() : "#D58AFF";
    }
    public static bool ValidHex(string? value) => value is not null && value.Trim().TrimStart('#') is var digits && digits.Length == 6 && digits.All(char.IsAsciiHexDigit) && value.Trim().Count(c=>c=='#') <= 1 && (!value.Contains('#') || value.Trim().StartsWith('#'));
    public static string Accent(string name, string custom) => Presets.FirstOrDefault(x=>x.Name==name)?.Hex ?? NormalizeHex(custom);
    public static string Rgb(int red,int green,int blue)
    { if (red is < 0 or > 255 || green is < 0 or > 255 || blue is < 0 or > 255) throw new ArgumentOutOfRangeException(); return $"#{red:X2}{green:X2}{blue:X2}"; }
    public static (int Red,int Green,int Blue) Components(string hex)
    { hex=NormalizeHex(hex); return (int.Parse(hex[1..3],NumberStyles.HexNumber),int.Parse(hex[3..5],NumberStyles.HexNumber),int.Parse(hex[5..7],NumberStyles.HexNumber)); }
    public static double Contrast(string first,string second)
    {
        double L(string value) { var (r,g,b)=Components(value); double Linear(int n) { double x=n/255d;return x<=0.04045?x/12.92:Math.Pow((x+0.055)/1.055,2.4); } return 0.2126*Linear(r)+0.7152*Linear(g)+0.0722*Linear(b); }
        double a=L(first),b=L(second); return (Math.Max(a,b)+0.05)/(Math.Min(a,b)+0.05);
    }
    public static string OnAccent(string accent) => Contrast(accent,"#000000")>=Contrast(accent,"#FFFFFF")?"#000000":"#FFFFFF";
    public static string ReadableAccent(string accent,string surface)
    {
        if(Contrast(accent,surface)>=4.5)return accent;
        var (r,g,b)=Components(accent); int end=OnAccent(surface)=="#000000"?0:255;
        for(int i=1;i<=100;i++) { double amount=i/100d; string candidate=Rgb((int)Math.Round(r+(end-r)*amount),(int)Math.Round(g+(end-g)*amount),(int)Math.Round(b+(end-b)*amount)); if(Contrast(candidate,surface)>=4.5)return candidate; }
        return end==0?"#000000":"#FFFFFF";
    }
}
