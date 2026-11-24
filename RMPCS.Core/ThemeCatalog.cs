using System.Globalization;
namespace RMPCS.Core;

public sealed record AccentPreset(string Name, string Hex);
public sealed record ThemePalette(string Window, string Surface, string Raised, string Text, string Muted, string Border, string Hover, string Selection, string SubtleBorder, string SwitchOff);
public static class ThemeCatalog
{
    public static readonly string[] Modes = ["System", "Light", "Dark"];
    public static IReadOnlyList<AccentPreset> Presets { get; } = Array.AsReadOnly(new[] {
        new AccentPreset("Midnight Purple", "#D58AFF"), new("Electric Blue", "#629BFF"), new("Neon Cyan", "#45DDEB"), new("Emerald Green", "#64E6BA"),
        new("Crimson Red", "#BE2B4E"), new("Sunset Orange", "#FFB66B"), new("Hot Pink", "#F07DBB"), new("Gold", "#EAC75F"),
        new("Ocean Slate", "#4EA5C7"), new("Nordic Frost", "#88A9D8"), new("Forest Mist", "#57A68A"),
        new("Warm Sand", "#B58B50"), new("Rose Quartz", "#C982A2"), new("Graphite", "#8E99A8") });
    public static ThemePalette Surfaces(string name, bool dark) => (name, dark) switch
    {
        ("Ocean Slate", true) => new("#101B24", "#15232C", "#20323F", "#F2F9FC", "#A9BBC5", "#647986", "#2C4655", "#294556", "#2D4654", "#405766"),
        ("Ocean Slate", false) => new("#F1F6F8", "#F9FCFD", "#E8F0F4", "#1C2F3B", "#526174", "#718395", "#E1EBF0", "#DFEAF4", "#D3E0E8", "#536B7C"),
        ("Nordic Frost", true) => new("#141B28", "#1B2535", "#263346", "#EFF5FC", "#B7C8DF", "#71839C", "#34475D", "#324963", "#35465E", "#4F627C"),
        ("Nordic Frost", false) => new("#F2F5FA", "#FBFCFF", "#E9EEF6", "#25354C", "#53657E", "#7A899E", "#E2E9F4", "#DCE5F2", "#D6DFEC", "#5A6C86"),
        ("Forest Mist", true) => new("#111E1A", "#192A24", "#233A31", "#F0F8F2", "#B2C9BC", "#718E7D", "#304C40", "#2C4A3C", "#355246", "#446853"),
        ("Forest Mist", false) => new("#F3F7F3", "#FCFEFB", "#EAF2E9", "#273D2C", "#536B59", "#7D927F", "#E0ECDF", "#DCEADC", "#D7E4D5", "#58755D"),
        ("Warm Sand", true) => new("#221C17", "#2E251E", "#3C3026", "#FCF5E9", "#D1BDA3", "#9B856C", "#4B3C2D", "#503E29", "#574736", "#796047"),
        ("Warm Sand", false) => new("#FAF6EE", "#FFFDF8", "#F4EDDF", "#433526", "#6E593F", "#99866C", "#EEE3D0", "#EBDFC7", "#E6D9C2", "#7A6548"),
        ("Rose Quartz", true) => new("#21171F", "#2D202A", "#3C2B37", "#FDF2FA", "#D4B5C9", "#9E7A92", "#4C3544", "#50364A", "#594052", "#79596F"),
        ("Rose Quartz", false) => new("#FBF4F8", "#FFFBFE", "#F6EAF1", "#4A3041", "#705368", "#9C8193", "#EFDFE9", "#EEDCE7", "#E8D4E1", "#7D5E74"),
        ("Graphite", true) => new("#171A1E", "#22262C", "#2E343C", "#F4F6F9", "#BEC7D2", "#818E9E", "#3A424D", "#3C4755", "#414A56", "#596575"),
        ("Graphite", false) => new("#F3F5F7", "#FDFEFE", "#EBEFF3", "#2D3641", "#546172", "#84909E", "#E0E6ED", "#DDE4EC", "#D8DFE7", "#627184"),
        (_, true) => new("#111019", "#171522", "#211E2D", "#F4F0FF", "#AEA6BD", "#655871", "#393047", "#352844", "#393141", "#51495F"),
        _ => new("#F5F7FA", "#FFFFFF", "#EDF1F6", "#202938", "#526174", "#8492A6", "#E7ECF3", "#E5EAF3", "#DCE2EB", "#637083")
    };
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
