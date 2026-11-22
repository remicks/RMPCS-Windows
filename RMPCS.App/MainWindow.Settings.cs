using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow
{
    private AppPreferences preferences = new();
    private readonly CancellationTokenSource windowLifetime = new();
    private ReleaseOffer? updateOffer;
    private bool checkingUpdates;
    internal static string AppVersion => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
    private void InitializePreferences()
    {
        string? warning = null;
        preferences = TestMode ? new() : SettingsStore.Load(out warning);
        StartPageChoice.ItemsSource = AppPreferences.Pages;
        ActivitySizeChoice.ItemsSource = Enumerable.Range(11, 8).ToArray();
        PutPreferenceControls(); ApplyPreferences();
        MainTabs.SelectedIndex = Array.IndexOf(AppPreferences.Pages, preferences.StartPage);
        VersionLabel.Text = $"Rusty's PC Toolkit • RMPCS {AppVersion}";
        RefreshInstallPreference(); RefreshHelp();
        SupportSummary.Text = $"Rusty's PC Toolkit / RMPCS {AppVersion}\nWindows: {Environment.OSVersion.Version}\n.NET: {Environment.Version}\nArchitecture: OS {RuntimeInformation.OSArchitecture}, process {RuntimeInformation.ProcessArchitecture}\nSession: {(admin ? "Administrator" : "Standard")}\nEdition: {(TaskSchedulerService.IsInstalled ? "Installed" : "Portable / development")}";
        Closed += (_, _) => { windowLifetime.Cancel(); windowLifetime.Dispose(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F1) { MainTabs.SelectedIndex = 6; e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.OemComma && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) { MainTabs.SelectedIndex = 5; e.Handled = true; }
        };
        if (warning is not null) Log(warning);
    }
    private void PutPreferenceControls()
    {
        CloseToTrayChoice.IsChecked = preferences.CloseToTray;
        MinimizeToTrayChoice.IsChecked = preferences.MinimizeToTray;
        TrayNotificationsChoice.IsChecked = preferences.TrayNotifications;
        CompletionNotificationsChoice.IsChecked = preferences.CompletionNotifications;
        RestoreBeforeRepairChoice.IsChecked = preferences.RestoreBeforeRepair;
        AutoUpdatesChoice.IsChecked = preferences.CheckUpdatesOnLaunch;
        PreviewUpdatesChoice.IsChecked = preferences.IncludePrereleases;
        StartPageChoice.SelectedItem = preferences.StartPage;
        ActivitySizeChoice.SelectedItem = preferences.ActivityFontSize;
        DefaultAgeChoice.SelectedIndex = preferences.CleanupAgeDays switch { 1 => 0, 30 => 2, _ => 1 };
    }
    private void ApplyPreferences()
    {
        Activity.FontSize = preferences.ActivityFontSize;
        Age.SelectedIndex = preferences.CleanupAgeDays switch { 1 => 1, 30 => 3, _ => 2 };
        RefreshTrayHint();
    }
    private void SavePreferences_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var next = new AppPreferences
        {
            CloseToTray = CloseToTrayChoice.IsChecked == true, MinimizeToTray = MinimizeToTrayChoice.IsChecked == true,
            TrayNotifications = TrayNotificationsChoice.IsChecked == true, CheckUpdatesOnLaunch = AutoUpdatesChoice.IsChecked == true,
            CompletionNotifications = CompletionNotificationsChoice.IsChecked == true,
            RestoreBeforeRepair = RestoreBeforeRepairChoice.IsChecked == true,
            IncludePrereleases = PreviewUpdatesChoice.IsChecked == true, StartPage = StartPageChoice.SelectedItem as string ?? "Health",
            ActivityFontSize = ActivitySizeChoice.SelectedItem is int size ? size : 12,
            CleanupAgeDays = DefaultAgeChoice.SelectedIndex switch { 0 => 1, 2 => 30, _ => 7 }
        }.Normalize();
        try
        {
            if (!TestMode) SettingsStore.Save(next);
            preferences = next; ApplyPreferences();
            PreferencesStatus.Text = "Preferences saved. Startup page and automatic update checks apply at the next launch. Current cleanup selections remain manual; review the age before running.";
        }
        catch { PreferencesStatus.Text = "Preferences could not be saved. Your previous preferences remain active."; }
    }
    private void ResetPreferences_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !Confirm("Reset interface, tray and update preferences to defaults?\n\nThis does not change startup/schedules, installed shortcuts, administrator preference, startup-entry backups or saved logs.")) return;
        try
        {
            var defaults = new AppPreferences(); if (!TestMode) SettingsStore.Save(defaults);
            preferences = defaults; PutPreferenceControls(); ApplyPreferences(); PreferencesStatus.Text = "Interface preferences reset. Automatic update checks are off.";
        }
        catch { PreferencesStatus.Text = "Preferences could not be reset."; }
    }
    private void RefreshInstallPreference()
    {
        bool installed = TaskSchedulerService.IsInstalled;
        InstallModeLabel.Text = installed ? "Installed edition. Desktop and Start Menu entries are managed by setup." : "Portable/development edition. Automatic elevation and sign-in tasks require the protected installed copy.";
        try { InstalledAdminChoice.IsChecked = installed && SettingsStore.AlwaysAdmin(); }
        catch { InstallModeLabel.Text += " Administrator preference unavailable."; }
        InstalledAdminChoice.IsEnabled = ApplyAdminPreference.IsEnabled = installed && admin && !busy;
    }
    private void ApplyAdminPreference_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !admin || !TaskSchedulerService.IsInstalled || TestMode) return;
        try { SettingsStore.SetAlwaysAdmin(InstalledAdminChoice.IsChecked == true); InstallModeLabel.Text = "Administrator preference saved for the installed copy across Windows users. Windows UAC remains active. It applies at the next launch."; }
        catch { InstallModeLabel.Text = "The administrator preference could not be saved. Repair the installation if its settings are missing."; }
    }
    internal async void BeginAutomaticUpdateCheck()
    {
        if (TestMode || !preferences.CheckUpdatesOnLaunch || !SettingsStore.TryClaimAutomaticCheck()) return;
        await CheckUpdatesAsync(preferences.IncludePrereleases);
    }
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(PreviewUpdatesChoice.IsChecked == true);
    private async Task CheckUpdatesAsync(bool includePreview)
    {
        if (checkingUpdates || TestMode) return;
        checkingUpdates = true; CheckUpdatesButton.IsEnabled = false; OpenUpdateButton.IsEnabled = false;
        updateOffer = null; UpdateNotes.Text = ""; UpdateStatus.Text = "Checking official GitHub releases...";
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false };
            using var client = new HttpClient(handler);
            var result = await new UpdateChecker(client).CheckAsync(AppVersion, includePreview, windowLifetime.Token);
            UpdateStatus.Text = $"{result.Message}\nChecked {DateTime.Now:g} • {(includePreview ? "Stable + preview" : "Stable only")}";
            updateOffer = result.Latest; OpenUpdateButton.IsEnabled = updateOffer is not null;
            UpdateNotes.Text = result.Latest?.Notes ?? "No release notes available for this check.";
            if (result.Newer) Log("A newer RMPCS release is available. Open Settings to review its notes and official release page.");
        }
        catch (OperationCanceledException) { if (!windowLifetime.IsCancellationRequested) UpdateStatus.Text = "GitHub check timed out. Try again or open Releases."; }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or System.IO.InvalidDataException or System.IO.IOException)
        { UpdateStatus.Text = "Update checking could not complete. Check your connection or open the official Releases page. No update status was confirmed."; }
        finally { checkingUpdates = false; CheckUpdatesButton.IsEnabled = true; }
    }
    private void OpenUpdate_Click(object sender, RoutedEventArgs e) { if (updateOffer is not null) OpenLink(updateOffer.Url); }
    private void Releases_Click(object sender, RoutedEventArgs e) => OpenLink(ProductInfo.Releases);
    private void Project_Click(object sender, RoutedEventArgs e) => OpenLink(ProductInfo.GitHub);
    private void Issues_Click(object sender, RoutedEventArgs e) => OpenLink(ProductInfo.Issues);
    private void LootDrop_Click(object sender, RoutedEventArgs e) => OpenLink(ProductInfo.LootDrop);
    private void Discord_Click(object sender, RoutedEventArgs e) => OpenLink(ProductInfo.Support);
    private void OpenLink(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0) return;
        bool allowed = target == ProductInfo.Support || (uri.Host == "github.com" && (uri.AbsolutePath == "/" + ProductInfo.Repository || uri.AbsolutePath.StartsWith("/" + ProductInfo.Repository + "/", StringComparison.Ordinal) || uri.AbsolutePath == "/remicks/LootDrop-Windows"));
        if (!allowed) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { Log("The browser could not be opened. Official project: " + target); }
    }
    private void AutomationSettings_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 2;
    private void ShowUpdates_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 5;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => UpdatesHeading.BringIntoView()));
    }
    private void HelpSearch_Changed(object sender, TextChangedEventArgs e) => RefreshHelp();
    private void RefreshHelp()
    {
        if (HelpResults is null || HelpBody is null || HelpSearch is null) return;
        var items = HelpCatalog.Search(HelpSearch.Text); HelpResults.ItemsSource = items;
        if (items.Count > 0) HelpResults.SelectedIndex = 0; else HelpBody.Text = "No matching help topics. Try a shorter search such as cleanup, updates, DNS, tray or administrator.";
    }
    private void HelpSelection_Changed(object sender, SelectionChangedEventArgs e)
    { if (HelpBody is not null && HelpResults.SelectedItem is HelpArticle article) HelpBody.Text = article.Body; }
    private void CopySupport_Click(object sender, RoutedEventArgs e)
    { try { Clipboard.SetText(SupportSummary.Text); Log("Support summary copied. Review it before sharing; health reports, startup commands and logs are excluded."); } catch { Log("Clipboard is unavailable. Select and copy the support summary text manually."); } }
    private void ClearActivity_Click(object sender, RoutedEventArgs e)
    { if (busy) return; Activity.Clear(); RunSummaryText.Clear(); SummaryExpander.Visibility = Visibility.Collapsed; Status.Text = "Activity view cleared. Saved run logs remain in History."; }
}
