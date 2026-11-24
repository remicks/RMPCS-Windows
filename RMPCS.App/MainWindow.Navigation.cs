using System.Windows;
using System.Windows.Input;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow
{
    private SectionHistory? sectionHistory;
    private bool goingBack;
    private bool navigationShortcutsWired;
    private void InitializeNavigation()
    {
        sectionHistory = new(AppPreferences.Pages[Math.Max(0, MainTabs.SelectedIndex)]);
        RefreshNavigation();
        if (navigationShortcutsWired) return;
        navigationShortcutsWired = true;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt) { Back_Click(this, new RoutedEventArgs()); e.Handled = true; }
        };
        PreviewMouseDown += (_, e) => { if (e.ChangedButton == MouseButton.XButton1) { Back_Click(this, new RoutedEventArgs()); e.Handled = true; } };
    }
    private void TrackSection()
    {
        if (sectionHistory is null || goingBack) return;
        string key = sidebarVisual && SidebarPageChoice.SelectedValue as string == "Results" ? "Results" : AppPreferences.Pages[Math.Max(0, MainTabs.SelectedIndex)];
        sectionHistory.Visit(key); RefreshNavigation();
    }
    private void RefreshNavigation()
    {
        if (BackButton is null || SidebarBackButton is null) return;
        BackButton.IsEnabled = SidebarBackButton.IsEnabled = sectionHistory?.CanGoBack == true;
        string key = sectionHistory?.Current ?? "Health";
        SectionTitle.Text = key == "Results" ? "Results & activity" : PageCatalog.All.FirstOrDefault(p => p.Key == key)?.Label ?? "Overview";
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        string? key = sectionHistory?.Back(); if (key is null) return;
        goingBack = true;
        try
        {
            if (key == "Results")
            {
                if (sidebarVisual) SidebarPageChoice.SelectedValue = key;
                else Activity.Focus();
            }
            else MainTabs.SelectedIndex = Array.IndexOf(AppPreferences.Pages, key);
            // Selecting the same retained tab does not raise SelectionChanged.
            if (key != "Results") { SyncSidebarPage(); UpdateSidebarPane(); }
        }
        finally { goingBack = false; RefreshNavigation(); }
    }
}
