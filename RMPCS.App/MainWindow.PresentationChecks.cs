using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow
{
    internal void VerifyPresentationUi(string output)
    {
        if (!TestMode) throw new InvalidOperationException("Presentation checks require an isolated test session.");
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        var initial = preferences;
        var plan = SelectedPlan();
        foreach (string name in FontCatalog.Names)
        {
            preferences = initial with { UiFont = name }; PutAppearanceControls(); ApplyPreferences(); UpdateLayout();
            Check(FontFamily.Source == FontManager.Family(name).Source && Activity.FontFamily.Source == FontFamily.Source, "Font choice reaches the window and output controls");
            if (name == "Windows system") continue;
            foreach (var weight in new[] { FontWeights.Normal, FontWeights.SemiBold })
            {
                var face = new Typeface(FontFamily, FontStyles.Normal, weight, FontStretches.Normal);
                Check(face.TryGetGlyphTypeface(out var glyph) && glyph.FontUri.Scheme == "pack" && glyph.FamilyNames.Values.Contains(name), "Bundled font resolves from application resources without Windows installation: " + name);
            }
        }
        foreach (string family in new[] { "barlow", "rajdhani", "atkinsonhyperlegible" })
        {
            using var license = Application.GetResourceStream(new Uri($"pack://application:,,,/FontLicenses/OFL-{family}.txt")).Stream;
            using var reader = new StreamReader(license);
            Check(reader.ReadToEnd().Contains("SIL OPEN FONT LICENSE"), "Bundled font copyright/license text is packaged");
        }
        preferences = initial; PutAppearanceControls(); ApplyPreferences();
        FontChoice.SelectedItem = "Rajdhani"; UpdateLayout();
        Check(FontFamily.Source == FontManager.Family("Rajdhani").Source && preferences.UiFont == initial.UiFont, "Font selection previews without saving preferences");
        DiscardTheme_Click(this, new RoutedEventArgs());
        Check(FontFamily.Source == FontManager.Family(initial.UiFont).Source && FontChoice.SelectedItem as string == initial.UiFont, "Discard restores the saved font and selection");
        MainTabs.SelectedIndex = 0; UpdateLayout();
        var originalSelectedBrush = Application.Current.Resources["SelectedTextBrush"];
        Application.Current.Resources["SelectedTextBrush"] = Brushes.Yellow; UpdateLayout();
        Check(((TabItem)MainTabs.Items[0]).Foreground is SolidColorBrush foreground && foreground.Color != Colors.Yellow, "Selected navigation text colors do not leak into page content");
        Application.Current.Resources["SelectedTextBrush"] = originalSelectedBrush;
        MainTabs.SelectedIndex = 0; InitializeNavigation();
        Check(!BackButton.IsEnabled && !SidebarBackButton.IsEnabled, "Back begins disabled in both layouts");
        MainTabs.SelectedIndex = 1; MainTabs.SelectedIndex = 5;
        Back_Click(this, new RoutedEventArgs()); Check(MainTabs.SelectedIndex == 1 && sectionHistory?.Current == "Maintenance", "Desktop Back returns to the previous section");
        Back_Click(this, new RoutedEventArgs()); Check(MainTabs.SelectedIndex == 0 && !BackButton.IsEnabled, "Back reaches the start without closing or hiding the window");
        preferences = initial with { SidebarEnabled = true }; PutSidebarControls(); ApplySidebarPreferences();
        MainTabs.SelectedIndex = 0; InitializeNavigation();
        SidebarPageChoice.SelectedValue = "Maintenance"; ShowSidebarResults();
        Check(sectionHistory?.Current == "Results", "Sidebar results are included in navigation history");
        Back_Click(this, new RoutedEventArgs()); Check(MainTabs.IsVisible && MainTabs.SelectedIndex == 1 && SidebarPageChoice.SelectedValue as string == "Maintenance", "Sidebar Back reveals the retained tool page");
        Back_Click(this, new RoutedEventArgs()); Check(MainTabs.SelectedIndex == 0 && !SidebarBackButton.IsEnabled, "Sidebar Back returns to overview without invoking Quit");
        preferences = initial; PutSidebarControls(); ApplySidebarPreferences();
        MainTabs.SelectedIndex = 5; UpdatesSection.IsExpanded = false; ShowUpdates_Click(this, new RoutedEventArgs());
        Check(UpdatesSection.IsExpanded, "Updates shortcut expands the grouped settings section");
        MainTabs.SelectedIndex = 1; UpdateLayout(); UserTemp.ApplyTemplate();
        Check(UserTemp.Template.FindName("Box", UserTemp) is Border { CornerRadius.TopLeft: 6 }, "Task checkbox uses the rounded theme template");
        MainTabs.SelectedIndex = 0; UpdateLayout(); Activity.ApplyTemplate();
        Check(Activity.Template.FindName("PART_ContentHost", Activity) is ScrollViewer, "Styled output retains WPF's editing/scroll host");
        Activity.Text = string.Join('\n', Enumerable.Range(1, 200).Select(i => $"Read-only fixture line {i}: Aa 0123456789"));
        Activity.Select(0, 9); Check(Activity.SelectedText == "Read-only" && Activity.IsReadOnly, "Styled read-only output remains selectable");
        Activity.ScrollToEnd(); UpdateLayout(); Check(Activity.VerticalOffset > 0, "Styled output supports long-report scrolling");
        Check(SelectedPlan() == plan && !busy && IsVisible, "Navigation and typography checks neither authorize maintenance nor exit the app");
        File.WriteAllText(output, $"{checks} bundled-font, Back navigation and styled-control checks passed. No font installation, preferences, maintenance or network requests were made.\n");
    }
}
