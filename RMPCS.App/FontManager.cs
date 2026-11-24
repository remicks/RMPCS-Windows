using System.Windows;
using System.Windows.Media;
using RMPCS.Core;

namespace RMPCS.App;

internal static class FontManager
{
    private static readonly Uri ResourceBase = new("pack://application:,,,/");
    internal static FontFamily Family(string name) => name == "Windows system"
        ? new FontFamily("Segoe UI Variable Text, Segoe UI")
        : new FontFamily(ResourceBase, "./Fonts/#" + FontCatalog.Normalize(name));
    internal static void Apply(AppPreferences preferences)
    {
        Application.Current.Resources["UiFontFamily"] = Family(preferences.UiFont);
        Application.Current.Resources["HeadingFontFamily"] = Family(preferences.UiFont == "Windows system" ? "Windows system" : preferences.UiFont);
    }
}
