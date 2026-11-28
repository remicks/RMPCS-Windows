using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow
{
    private MonitoringOverlay? monitoringOverlay;
    private HwndSource? overlayShortcutSource;
    private bool overlayReady, overlayHotkeyRegistered;
    private double overlayX = .95, overlayY = .05;
    private OverlayPreferences overlayViewDraft = new OverlayPreferences().Normalize();
    private int overlayViewIndex;
    private bool loadingOverlayViews;
    private const int OverlayHotkeyId = 0x524D;
    private void InitializeOverlay()
    {
        OverlayIntervalChoice.ItemsSource = new[] { 1, 2, 5 };
        OverlayTextChoice.ItemsSource = new[] { 12, 14, 16, 18 };
        OverlayWidthChoice.ItemsSource = new[] { 220, 280, 340 };
        OverlayOpacityChoice.ItemsSource = new[] { 50, 60, 70, 80, 90, 100 };
        OverlayFontChoice.ItemsSource = new[] { "App font" }.Concat(FontCatalog.Names).ToArray();
        OverlayPositionChoice.ItemsSource = OverlayPreferences.Positions;
        OverlayViewMetricChoice.ItemsSource = new[] { "CPU", "RAM", "Network (download and upload)", "System drive available", "Windows uptime", "Temperature sensors" };
        InitializeTemperatureControls();
        OverlayNetworkScaleChoice.ItemsSource = new[] { 1, 10, 100, 1000 };
        RefreshOverlayDisplays(preferences.Overlay.Monitor);
        SourceInitialized += (_, _) =>
        {
            overlayShortcutSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            overlayShortcutSource?.AddHook(OverlayShortcut);
        };
        Loaded += (_, _) => { if (overlayReady) return; overlayReady = true; ApplyOverlayPreferences(true); };
        Closed += (_, _) =>
        {
            SetOverlayHotkey(false); overlayShortcutSource?.RemoveHook(OverlayShortcut);
            monitoringOverlay?.Dispose(); monitoringOverlay = null;
        };
    }
    private void RefreshOverlayDisplays(string key)
    {
        try
        {
            var displays = SidebarDock.Choices().ToList();
            if (!displays.Any(d => d.Key == key)) displays.Add(new(key, "Disconnected display (current display fallback)"));
            OverlayMonitorChoice.ItemsSource = displays;
        }
        catch { OverlayMonitorChoice.ItemsSource = new[] { new MonitorChoice("Current", "Current monitor") }; }
        OverlayMonitorChoice.SelectedValue = key;
    }
    private void PutOverlayControls()
    {
        var options = preferences.Overlay.Normalize(); overlayX = options.X; overlayY = options.Y;
        OverlayTransparentChoice.IsChecked=options.TransparentBackground;OverlayWidgetsChoice.IsChecked=options.Widgets;widgetLayoutDraft=options.WidgetLayoutJson;PutTemperatureControls();
        OverlayStartupChoice.IsChecked = options.Enabled; OverlayCpuChoice.IsChecked = options.Cpu;
        OverlayMemoryChoice.IsChecked = options.Memory; OverlayNetworkChoice.IsChecked = options.Network;
        OverlayDiskChoice.IsChecked = options.DiskSpace; OverlayUptimeChoice.IsChecked = options.Uptime;
        loadingOverlayViews = true; overlayViewDraft = options; overlayViewIndex = 0;
        OverlayViewMetricChoice.SelectedIndex = 0; LoadOverlayViews(); loadingOverlayViews = false;
        OverlayNetworkScaleChoice.SelectedItem = options.NetworkScaleMiB;
        OverlayTopmostChoice.IsChecked = options.Topmost;
        OverlayLockedChoice.IsChecked = options.Locked; OverlayClickThroughChoice.IsChecked = options.ClickThrough;
        OverlayHotkeyChoice.IsChecked = options.Hotkey; OverlayIntervalChoice.SelectedItem = options.IntervalSeconds;
        OverlayTextChoice.SelectedItem = options.TextSize; OverlayWidthChoice.SelectedItem = options.Width;
        OverlayOpacityChoice.SelectedItem = options.OpacityPercent; OverlayFontChoice.SelectedItem = options.Font;
        OverlayPositionChoice.SelectedItem = options.Position; RefreshOverlayDisplays(options.Monitor);
    }
    private MetricViewPreferences DraftViews() => overlayViewIndex switch { 1 => overlayViewDraft.MemoryView!, 2 => overlayViewDraft.NetworkView!, 3 => overlayViewDraft.DiskView!, 4 => overlayViewDraft.UptimeView!, 5 => overlayViewDraft.Temperatures.Views, _ => overlayViewDraft.CpuView! };
    private void LoadOverlayViews()
    {
        var views = DraftViews();
        OverlayViewNumbers.IsChecked = views.Numbers; OverlayViewCircle.IsChecked = views.Circle;
        OverlayViewCircleValue.IsChecked = views.CircleWithValue; OverlayViewBar.IsChecked = views.Bar; OverlayViewGraph.IsChecked = views.Graph;
    }
    private void CaptureOverlayViews()
    {
        var views = new MetricViewPreferences { Numbers = OverlayViewNumbers.IsChecked == true, Circle = OverlayViewCircle.IsChecked == true,
            CircleWithValue = OverlayViewCircleValue.IsChecked == true, Bar = OverlayViewBar.IsChecked == true, Graph = OverlayViewGraph.IsChecked == true };
        overlayViewDraft = overlayViewIndex switch { 1 => overlayViewDraft with { MemoryView = views }, 2 => overlayViewDraft with { NetworkView = views },
            3 => overlayViewDraft with { DiskView = views }, 4 => overlayViewDraft with { UptimeView = views }, 5 => overlayViewDraft with { Temperatures=overlayViewDraft.Temperatures with {Views=views} }, _ => overlayViewDraft with { CpuView = views } };
    }
    private void OverlayViewMetric_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (loadingOverlayViews || OverlayViewNumbers is null || OverlayViewMetricChoice.SelectedIndex < 0) return;
        CaptureOverlayViews(); overlayViewIndex = OverlayViewMetricChoice.SelectedIndex; LoadOverlayViews();
    }
    private OverlayPreferences OverlayChoices()
    {
        CaptureOverlayViews();
        return new OverlayPreferences
        {
        TransparentBackground=OverlayTransparentChoice.IsChecked==true,Widgets=OverlayWidgetsChoice.IsChecked==true,WidgetLayoutJson=widgetLayoutDraft,Temperatures=TemperatureChoices(overlayViewDraft.Temperatures.Views),
        Enabled = OverlayStartupChoice.IsChecked == true, Cpu = OverlayCpuChoice.IsChecked == true,
        Memory = OverlayMemoryChoice.IsChecked == true, Network = OverlayNetworkChoice.IsChecked == true,
        DiskSpace = OverlayDiskChoice.IsChecked == true, Uptime = OverlayUptimeChoice.IsChecked == true,
        CpuView = overlayViewDraft.CpuView, MemoryView = overlayViewDraft.MemoryView, NetworkView = overlayViewDraft.NetworkView,
        DiskView = overlayViewDraft.DiskView, UptimeView = overlayViewDraft.UptimeView,
        NetworkScaleMiB = OverlayNetworkScaleChoice.SelectedItem is int scale ? scale : 10, Topmost = OverlayTopmostChoice.IsChecked == true,
        Locked = OverlayLockedChoice.IsChecked == true, ClickThrough = OverlayClickThroughChoice.IsChecked == true,
        Hotkey = OverlayHotkeyChoice.IsChecked == true, IntervalSeconds = OverlayIntervalChoice.SelectedItem is int interval ? interval : 2,
        TextSize = OverlayTextChoice.SelectedItem is int text ? text : 14, Width = OverlayWidthChoice.SelectedItem is int width ? width : 280,
        OpacityPercent = OverlayOpacityChoice.SelectedItem is int opacity ? opacity : 90, Font = OverlayFontChoice.SelectedItem as string ?? "App font",
        Monitor = OverlayMonitorChoice.SelectedValue as string ?? "Current", Position = OverlayPositionChoice.SelectedItem as string ?? "Top right", X = overlayX, Y = overlayY
        }.Normalize();
    }
    private void ApplyOverlayPreferences(bool startup = false)
    {
        if (!overlayReady || TestMode) return;
        if (startup ? preferences.Overlay.Enabled : monitoringOverlay?.SessionVisible == true) ShowOverlay(preferences.Overlay);
        else SetOverlayHotkey(preferences.Overlay.Hotkey && overlayHotkeyRegistered);
    }
    private void ResetOverlaySession() { monitoringOverlay?.HideSession(); SetOverlayHotkey(false); }
    private void ShowOverlay(OverlayPreferences options)
    {
        if (TestMode) return;
        try
        {
            if (monitoringOverlay is null)
            {
                monitoringOverlay = new MonitoringOverlay(this, false);
                monitoringOverlay.Issue += message => OverlayStatus.Text = message;
                monitoringOverlay.SessionVisibilityChanged += () => { if (monitoringOverlay?.SessionVisible == false) OverlayStatus.Text = "Overlay/widgets hidden; sampling stopped. Reopen from Settings, tray or shortcut."; };
            }
            monitoringOverlay.Configure(options); monitoringOverlay.ShowSession();
            if (monitoringOverlay.Error is string error) { monitoringOverlay.HideSession(); OverlayStatus.Text = error; return; }
            SetOverlayHotkey(options.Hotkey);
            OverlayStatus.Text = "Overlay visible. Local counters only; no monitoring history is saved. " +
                (options.Hotkey ? overlayHotkeyRegistered ? "Ctrl+Alt+O toggles visibility." : "Shortcut unavailable; use Settings or the tray." : "Shortcut off; use Settings or the tray.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            monitoringOverlay?.Dispose(); monitoringOverlay = null; SetOverlayHotkey(false);
            OverlayStatus.Text = "Overlay unavailable in this session. No maintenance was started.";
        }
    }
    internal void ToggleOverlayFromTray()
    {
        if (TestMode) return;
        if (monitoringOverlay?.SessionVisible == true) { monitoringOverlay.HideSession(); OverlayStatus.Text = "Overlay/widgets hidden; sampling stopped. Use Settings, tray or shortcut to show them again."; }
        else ShowOverlay(preferences.Overlay);
    }
    private void OverlayToggle_Click(object sender, RoutedEventArgs e)
    {
        if (TestMode) return;
        if (monitoringOverlay?.SessionVisible == true) ToggleOverlayFromTray(); else ShowOverlay(OverlayChoices());
    }
    private void OverlayApply_Click(object sender, RoutedEventArgs e)
    {
        if (busy || TestMode) return;
        try
        {
            var next = preferences with { Overlay = OverlayChoices() };
            SettingsStore.Save(next); preferences = next.Normalize(); PutOverlayControls();
            if (next.Overlay.Enabled || monitoringOverlay?.SessionVisible == true) ShowOverlay(preferences.Overlay);
            else SetOverlayHotkey(preferences.Overlay.Hotkey && overlayHotkeyRegistered);
            if (!next.Overlay.Enabled) OverlayStatus.Text = "Overlay settings saved. Automatic display is off. " + (monitoringOverlay?.SessionVisible == true ? "The current overlay/widgets stay visible until hidden." : "Show / hide opens them for this session.");
        }
        catch { OverlayStatus.Text = "Overlay settings could not be saved. Previous saved choices remain active."; }
    }
    private void OverlayRemember_Click(object sender, RoutedEventArgs e)
    {
        if (monitoringOverlay?.SessionVisible != true) { OverlayStatus.Text = "Show the overlay/widgets and drag them into place first."; return; }
        try
        {
            if(OverlayWidgetsChoice.IsChecked==true){widgetLayoutDraft=monitoringOverlay.CaptureWidgetLayouts();OverlayStatus.Text="Widget positions and individual locks copied into the form. Apply overlay settings to keep them.";return;}
            var position = monitoringOverlay.CapturePosition(); overlayX = position.X; overlayY = position.Y;
            OverlayLockedChoice.IsChecked=position.Locked;
            RefreshOverlayDisplays(position.Monitor); OverlayPositionChoice.SelectedItem = "Custom";
            OverlayStatus.Text = "Current position copied into the form. Apply overlay settings to keep it.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
        { OverlayStatus.Text = "Current display position is unavailable. Refresh displays and choose a corner instead."; }
    }
    private void OverlayDisplays_Click(object sender, RoutedEventArgs e) => RefreshOverlayDisplays(OverlayMonitorChoice.SelectedValue as string ?? "Current");
    private void SetupGuide_Click(object sender, RoutedEventArgs e)
    {
        if (!busy && !TestMode) new SetupGuidePreview(this).ShowDialog();
    }
    private void SetOverlayHotkey(bool enabled)
    {
        if (TestMode || overlayShortcutSource is null) return;
        if (!enabled && overlayHotkeyRegistered) { UnregisterHotKey(overlayShortcutSource.Handle, OverlayHotkeyId); overlayHotkeyRegistered = false; }
        else if (enabled && !overlayHotkeyRegistered) overlayHotkeyRegistered = RegisterHotKey(overlayShortcutSource.Handle, OverlayHotkeyId, 0x4003, 0x4F); // NOREPEAT | CTRL | ALT, O
    }
    private IntPtr OverlayShortcut(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt64() == OverlayHotkeyId && overlayHotkeyRegistered) { ToggleOverlayFromTray(); handled = true; }
        return IntPtr.Zero;
    }
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
