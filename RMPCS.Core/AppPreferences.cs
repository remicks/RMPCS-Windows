using System.Text.Json;

namespace RMPCS.Core;

public sealed record AppPreferences
{
    public bool CloseToTray { get; init; } = true;
    public bool MinimizeToTray { get; init; } = true;
    public bool TrayNotifications { get; init; } = true;
    public bool CompletionNotifications { get; init; }
    public bool RestoreBeforeRepair { get; init; }
    public bool CheckUpdatesOnLaunch { get; init; }
    public bool IncludePrereleases { get; init; } = true;
    public int ActivityFontSize { get; init; } = 12;
    public int CleanupAgeDays { get; init; } = 7;
    public string ThemeMode { get; init; } = "System";
    public string UiFont { get; init; } = "Barlow";
    public string AccentName { get; init; } = "Crimson Red";
    public string CustomAccent { get; init; } = "#D58AFF";
    public string StartPage { get; init; } = "Health";
    public bool SidebarEnabled { get; init; }
    public bool SidebarReserveSpace { get; init; }
    public string SidebarEdge { get; init; } = "Right";
    public string SidebarMonitor { get; init; } = "Current";
    public int SidebarWidth { get; init; } = 400;
    public static readonly string[] Pages = ["Health", "Maintenance", "Automation", "Startup", "History", "Settings", "Help", "Diagnostics", "Protection"];
    public AppPreferences Normalize() => this with
    {
        ThemeMode = ThemeCatalog.Modes.Contains(ThemeMode, StringComparer.Ordinal) ? ThemeMode : "System",
        UiFont = FontCatalog.Normalize(UiFont),
        AccentName = AccentName == "Custom" || ThemeCatalog.Presets.Any(x=>x.Name==AccentName) ? AccentName : "Crimson Red",
        CustomAccent = ThemeCatalog.NormalizeHex(CustomAccent),
        ActivityFontSize = ActivityFontSize is >= 11 and <= 18 ? ActivityFontSize : 12,
        CleanupAgeDays = CleanupAgeDays is 1 or 7 or 30 ? CleanupAgeDays : 7,
        StartPage = Pages.Contains(StartPage, StringComparer.Ordinal) ? StartPage : "Health",
        SidebarEdge = SidebarEdge is "Left" or "Right" ? SidebarEdge : "Right",
        SidebarMonitor = SidebarLayout.ValidMonitor(SidebarMonitor) ? SidebarMonitor : "Current",
        SidebarWidth = SidebarWidth is 360 or 400 or 440 or 480 ? SidebarWidth : 400
    };
    public static AppPreferences Parse(string json)
    {
        if (json.Length > 8192) throw new InvalidDataException("Preference data exceeds the supported size.");
        return (JsonSerializer.Deserialize<AppPreferences>(json, new JsonSerializerOptions { MaxDepth = 8 }) ?? new()).Normalize();
    }
    public string ToJson() => JsonSerializer.Serialize(Normalize());
}
