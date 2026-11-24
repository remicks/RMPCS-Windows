using RMPCS.Core;

internal static class PresentationChecks
{
    public static void Run(Action<bool, string> check)
    {
        check(FontCatalog.Names.Count == 4 && FontCatalog.Normalize("https://example.test/font.ttf") == "Barlow" && FontCatalog.Normalize(null) == "Barlow", "font preferences accept only bundled families or the system option, never a path or URL");
        foreach (string family in FontCatalog.Names)
        {
            var preference = new AppPreferences { UiFont = family };
            check(AppPreferences.Parse(preference.ToJson()).UiFont == family, "font choice persists: " + family);
            check(new PreferenceUndo(new(), preference).Restore(preference) == new AppPreferences(), "font choice participates in preference undo: " + family);
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
