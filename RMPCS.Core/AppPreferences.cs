using System.Text.Json;

namespace RMPCS.Core;

public sealed record AppPreferences
{
    public bool CloseToTray { get; init; } = true;
    public bool MinimizeToTray { get; init; } = true;
    public bool TrayNotifications { get; init; } = true;
    public bool CompletionNotifications { get; init; }
    public bool CheckUpdatesOnLaunch { get; init; }
    public bool IncludePrereleases { get; init; } = true;
    public int ActivityFontSize { get; init; } = 12;
    public int CleanupAgeDays { get; init; } = 7;
    public string StartPage { get; init; } = "Health";
    public static readonly string[] Pages = ["Health", "Maintenance", "Automation", "Startup", "History", "Settings", "Help"];
    public AppPreferences Normalize() => this with
    {
        ActivityFontSize = ActivityFontSize is >= 11 and <= 18 ? ActivityFontSize : 12,
        CleanupAgeDays = CleanupAgeDays is 1 or 7 or 30 ? CleanupAgeDays : 7,
        StartPage = Pages.Contains(StartPage, StringComparer.Ordinal) ? StartPage : "Health"
    };
    public static AppPreferences Parse(string json)
    {
        if (json.Length > 8192) throw new InvalidDataException("Preference data exceeds the supported size.");
        return (JsonSerializer.Deserialize<AppPreferences>(json, new JsonSerializerOptions { MaxDepth = 8 }) ?? new()).Normalize();
    }
    public string ToJson() => JsonSerializer.Serialize(Normalize());
}
