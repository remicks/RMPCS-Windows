using System.Windows;
using System.Windows.Controls;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow
{
    private SidebarDock? sidebarDock;
    private bool syncingSidebar, sidebarVisual;
    private void InitializeSidebar()
    {
        SidebarEdgeChoice.ItemsSource = new[] { "Left", "Right" };
        SidebarWidthChoice.ItemsSource = new[] { 360, 400, 440, 480 };
        SidebarPageChoice.ItemsSource = PageCatalog.All.Append(new PageOption("Results", "Results & activity")).ToArray();
        RefreshSidebarMonitors(preferences.SidebarMonitor);
        SourceInitialized += (_, _) =>
        {
            try { sidebarDock = new SidebarDock(this, text => SidebarStatus.Text = text); sidebarDock.Configure(preferences); }
            catch { SidebarStatus.Text = "Sidebar docking is unavailable. Use Full window to restore the standard layout."; }
        };
        Closed += (_, _) => sidebarDock?.Dispose();
    }
    private void RefreshSidebarMonitors(string selected)
    {
        try
        {
            var choices = SidebarDock.Choices().ToList();
            if (!choices.Any(c => c.Key == selected)) choices.Add(new(selected, $"{selected} · disconnected (fallback display will be used)"));
            SidebarMonitorChoice.ItemsSource = choices;
        }
        catch { SidebarMonitorChoice.ItemsSource = new[] { new MonitorChoice("Current", "Current monitor") }; }
        SidebarMonitorChoice.SelectedValue = selected;
    }
    private void PutSidebarControls()
    {
        SidebarEnabledChoice.IsChecked = preferences.SidebarEnabled;
        SidebarReserveChoice.IsChecked = preferences.SidebarReserveSpace;
        SidebarEdgeChoice.SelectedItem = preferences.SidebarEdge;
        SidebarWidthChoice.SelectedItem = preferences.SidebarWidth;
        RefreshSidebarMonitors(preferences.SidebarMonitor);
    }
    private AppPreferences SidebarChoices(AppPreferences basis) => basis with
    {
        SidebarEnabled = SidebarEnabledChoice.IsChecked == true,
        SidebarReserveSpace = SidebarReserveChoice.IsChecked == true,
        SidebarEdge = SidebarEdgeChoice.SelectedItem as string ?? "Right",
        SidebarMonitor = SidebarMonitorChoice.SelectedValue as string ?? "Current",
        SidebarWidth = SidebarWidthChoice.SelectedItem is int width ? width : 400
    };
    private void ApplySidebarPreferences()
    {
        bool enabled = preferences.SidebarEnabled;
        if (enabled != sidebarVisual)
        {
            if (enabled) sidebarDock?.CaptureRestore();
            sidebarVisual = enabled;
            DesktopHeader.Visibility = TrayHintPanel.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            SidebarHeader.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            RootLayout.Margin = new Thickness(enabled ? 12 : 26);
            ResultsHint.Text = enabled ? "Need a hand? Help explains each tool. Choose Results & activity above to review output." : "Need a hand? Help explains each tool. Results appear on the right.";
            MainTabs.Padding = new Thickness(enabled ? 12 : 16);
            ResultsPanel.Padding = new Thickness(enabled ? 12 : 18);
            Grid.SetColumn(ResultsPanel, enabled ? 0 : 2);
            Grid.SetColumnSpan(ResultsPanel, enabled ? 3 : 1);
            Grid.SetColumnSpan(MainTabs, enabled ? 3 : 1);
            if (enabled)
            {
                // SelectedContent is the original page: no duplicate controls or execution paths.
                MainTabs.Template = (ControlTemplate)FindResource("SidebarTabs");
                MinWidth = 0; MinHeight = 0; ResizeMode = ResizeMode.NoResize;
                if (WindowState != WindowState.Minimized) WindowState = WindowState.Normal;
            }
            else
            {
                MainTabs.ClearValue(TemplateProperty);
                MinWidth = Math.Min(960, SystemParameters.WorkArea.Width); MinHeight = Math.Min(620, SystemParameters.WorkArea.Height);
                ResizeMode = ResizeMode.CanResize;
            }
            SyncSidebarPage();
        }
        SidebarElevateButton.Visibility = enabled && !admin ? Visibility.Visible : Visibility.Collapsed;
        sidebarDock?.Configure(preferences);
        if (!enabled) SidebarStatus.Text = "Sidebar is off. These choices do not start maintenance.";
        UpdateSidebarPane();
        if (enabled && busy) ShowSidebarResults();
    }
    private void SyncSidebarPage()
    {
        if (SidebarPageChoice is null || syncingSidebar) return;
        syncingSidebar = true;
        SidebarPageChoice.SelectedValue = MainTabs.SelectedIndex >= 0 ? AppPreferences.Pages[MainTabs.SelectedIndex] : "Health";
        syncingSidebar = false;
        UpdateSidebarPane();
    }
    private void UpdateSidebarPane()
    {
        if (ResultsPanel is null) return;
        bool results = sidebarVisual && SidebarPageChoice.SelectedValue as string == "Results";
        MainTabs.Visibility = results ? Visibility.Collapsed : Visibility.Visible;
        ResultsPanel.Visibility = !sidebarVisual || results ? Visibility.Visible : Visibility.Collapsed;
        RunButton.Visibility = !results && MainTabs.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SidebarPage_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncingSidebar || MainTabs is null) return;
        string key = SidebarPageChoice.SelectedValue as string ?? "Health";
        if (key != "Results")
        {
            syncingSidebar = true; MainTabs.SelectedIndex = Array.IndexOf(AppPreferences.Pages, key); syncingSidebar = false;
        }
        UpdateSidebarPane();
    }
    private void ShowSidebarResults() { if (sidebarVisual) SidebarPageChoice.SelectedValue = "Results"; }
    private void SaveSidebar(AppPreferences next)
    {
        try
        {
            next = next.Normalize(); if (!TestMode) SettingsStore.Save(next);
            preferences = next; PutSidebarControls(); ApplySidebarPreferences();
        }
        catch { SidebarStatus.Text = "Sidebar preferences could not be saved. The previous settings remain active."; }
    }
    private void EnterSidebar_Click(object sender, RoutedEventArgs e) => SaveSidebar(preferences with { SidebarEnabled = true });
    private void LeaveSidebar_Click(object sender, RoutedEventArgs e) => SaveSidebar(preferences with { SidebarEnabled = false });
    private void ApplySidebar_Click(object sender, RoutedEventArgs e) { if (!busy) SaveSidebar(SidebarChoices(preferences)); }
    private void RefreshDisplays_Click(object sender, RoutedEventArgs e) => RefreshSidebarMonitors(SidebarMonitorChoice.SelectedValue as string ?? preferences.SidebarMonitor);
}
