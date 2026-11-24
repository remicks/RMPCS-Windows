using RMPCS.Core;

internal static class PresentationChecks
{
    public static void Run(Action<bool, string> check)
    {
        check(FontCatalog.Names.Count == 9 && FontCatalog.LicenseIds.Count == 8 && FontCatalog.Normalize("https://example.test/font.ttf") == "Barlow" && FontCatalog.Normalize(null) == "Barlow", "eight bundled families and the system option remain allowlisted, never a path or URL");
        foreach (string family in FontCatalog.Names)
        {
            var preference = new AppPreferences { UiFont = family };
            check(AppPreferences.Parse(preference.ToJson()).UiFont == family, "font choice persists: " + family);
            check(new PreferenceUndo(new(), preference).Restore(preference) == new AppPreferences(), "font choice participates in preference undo: " + family);
        }
        foreach (var preset in ThemeCatalog.Presets.Skip(8))
        foreach (bool dark in new[] { false, true })
        {
            var palette = ThemeCatalog.Surfaces(preset.Name, dark);
            var preference = new AppPreferences { AccentName = preset.Name, ThemeMode = dark ? "Dark" : "Light" };
            check(AppPreferences.Parse(preference.ToJson()) == preference, "coordinated theme persists: " + preset.Name);
            foreach (string surface in new[] { palette.Window, palette.Surface, palette.Raised, palette.Hover, palette.Selection })
                check(ThemeCatalog.Contrast(palette.Text, surface) >= 4.5 && ThemeCatalog.Contrast(palette.Muted, surface) >= 4.5, "body and secondary text retain contrast: " + preset.Name + (dark ? " Dark" : " Light") + " " + surface);
            check(ThemeCatalog.Contrast(palette.Border, palette.Surface) >= 3 && ThemeCatalog.Contrast("#FFFFFF", palette.SwitchOff) >= 3, "control edges and off-state switch thumb remain distinct: " + preset.Name);
            check(ThemeCatalog.Contrast(ThemeCatalog.ReadableAccent(preset.Hex, palette.Surface), palette.Surface) >= 4.5, "accent text adapts to the coordinated surface: " + preset.Name);
        }
        var history = new SectionHistory("Health");
        check(!history.CanGoBack && history.Back() is null, "Back on the initial page is inert");
        history.Visit("Health"); history.Visit("arbitrary-command");
        check(!history.CanGoBack, "duplicate and unknown navigation entries cannot create actions or history");
        history.Visit("Maintenance"); history.Visit("Settings"); history.Visit("Results");
        check(history.Back() == "Settings" && history.Back() == "Maintenance" && history.Back() == "Health" && !history.CanGoBack, "Back traverses sections and results in reverse order without replaying visits");
        history.Visit("Help"); history.Visit("Health"); history.Back(); history.Visit("Diagnostics");
        check(history.Back() == "Help" && history.Back() == "Health", "new navigation after Back follows the visited branch");
        for (int i = 0; i < 1000; i++) history.Visit(i % 2 == 0 ? "Help" : "Health");
        int count = 0; while (history.Back() is not null) count++;
        check(count == 64, "navigation memory is bounded");
    }
}
