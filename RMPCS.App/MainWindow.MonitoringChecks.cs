using System.IO;
using System.Windows;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow
{
    internal async Task VerifyMonitoringUiAsync(string output)
    {
        if (!TestMode) throw new InvalidOperationException("Monitoring UI checks require an isolated test session.");
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        var initial = preferences;
        try
        {
            Check(monitoringOverlay is null && !overlayHotkeyRegistered && !preferences.Overlay.Enabled, "default/test startup creates no overlay or global shortcut");
            preferences = initial with { Overlay = new() { Enabled = true, TextSize = 18, ClickThrough = true, Locked = true, IntervalSeconds = 5 } };
            PutPreferenceControls(); var retained = preferences.Overlay.Normalize();
            SavePreferences_Click(this, new RoutedEventArgs());
            Check(preferences.Overlay == retained && monitoringOverlay is null, "saving unrelated preferences preserves overlay choices; test mode never launches it");
            preferences = initial; PutPreferenceControls(); ApplyPreferences();
            using (var overlay = new MonitoringOverlay(this, true))
            {
                overlay.Configure(new()); overlay.Show(); await Task.Delay(150);
                Check(overlay.IsVisible && overlay.Error is null && !overlay.Sampling, "synthetic overlay is visible with sampling disabled");
                Check((overlay.StyleForChecks & 0x08000080L) == 0x08000080L && !overlay.ShowActivated && !overlay.ShowInTaskbar, "native tool window does not activate or add a taskbar entry");
                for (int i = 0; i < 200; i++) overlay.Render(new(i % 100, 40, 4, 10, 1000, 500, 1000, 10000));
                Check(overlay.HistoryCount == 60, "graph history remains bounded after extended sampling fixtures");
                overlay.Hide(); Check(overlay.HistoryCount == 0 && !overlay.Sampling, "hiding clears in-memory history and stops sampling");
                for (int mask = 0; mask < 32; mask++)
                {
                    var views = new MetricViewPreferences { Numbers = (mask & 1) != 0, Circle = (mask & 2) != 0, CircleWithValue = (mask & 4) != 0, Bar = (mask & 8) != 0, Graph = (mask & 16) != 0 };
                    overlay.Configure(new() { CpuView = views, MemoryView = views, NetworkView = views, DiskView = views, UptimeView = views });
                    overlay.Show(); overlay.Render(new(50, 40, 4, 10, 1000, 500, 1000, 10000, 25)); overlay.UpdateLayout();
                    foreach (var display in overlay.DisplaysForChecks)
                        Check(display.VisibleViews.SequenceEqual(new[] { views.Numbers, views.Circle, views.CircleWithValue, views.Bar, views.Graph }), "every metric supports any independent combination of the five displays");
                    await Task.Delay(1); // Render actual gauge geometry without enabling live sampling.
                }
                var valueOnly = new MetricViewPreferences { Numbers = false, CircleWithValue = true };
                overlay.Configure(new() { CpuView = valueOnly });
                Check(!overlay.DisplaysForChecks[0].VisibleViews[0] && overlay.DisplaysForChecks[0].VisibleViews[2], "inside-circle values remain visible with standalone numbers off");
                overlay.Render(new(null, null, null, null, null, null, null, 0));
                Check(overlay.DisplaysForChecks.Take(5).All(d => d.UnknownGauge), "unavailable readings retain unknown gauge state rather than a fake zero");
                overlay.Configure(new() { Cpu = false, CpuView = new() { Numbers = true, Circle = true, CircleWithValue = true, Bar = true, Graph = true } });
                Check(overlay.DisplaysForChecks[0].Visibility == Visibility.Collapsed, "disabling a metric hides its views without changing their saved configuration");
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    ThemeManager.Apply(initial with { ThemeMode = theme });
                    overlay.Configure(new() { ClickThrough = true, Locked = true, OpacityPercent = 50, Font = "Atkinson Hyperlegible" });
                    overlay.Show(); await Task.Delay(80);
                    Check((overlay.StyleForChecks & 0x20) != 0 && overlay.Opacity == (SystemParameters.HighContrast ? 1 : .5), "click-through and High Contrast aware opacity are applied");
                    Check(overlay.FontFamily.Source.Contains("Atkinson Hyperlegible", StringComparison.Ordinal), "overlay font uses the selected bundled family");
                    overlay.Configure(new() { ClickThrough = false });
                    Check((overlay.StyleForChecks & 0x20) == 0, "click-through can be reversed without injecting input");
                }
                foreach (var screen in SidebarDock.Screens())
                {
                    overlay.Configure(new() { Monitor = screen.Device, Position = "Bottom right", Width = 340, TextSize = 18 });
                    await Task.Delay(150); var rect = overlay.BoundsForChecks;
                    Check(rect.Left >= screen.Work.Left - 2 && rect.Top >= screen.Work.Top - 2 && rect.Right <= screen.Work.Right + 2 && rect.Bottom <= screen.Work.Bottom + 2, "overlay stays within selected monitor work area");
                }
                overlay.Configure(new() { Monitor = @"\\.\DISPLAY999999" }); await Task.Delay(100);
                Check(overlay.IsVisible && overlay.Error is null && !overlay.Sampling, "disconnected monitor falls back without starting test sampling");
                ThemeManager.Apply(initial with { ThemeMode = "Dark" });
                var previewViews = new MetricViewPreferences { Numbers = true, CircleWithValue = true, Bar = true, Graph = true };
                overlay.Configure(new() { Width = 340, CpuView = previewViews, MemoryView = previewViews, Network = false, DiskSpace = false, Uptime = false });
                for (int i = 0; i < 60; i++)
                {
                    double percent = 38 + i % 12; ulong total = 32UL * 1073741824;
                    overlay.Render(new(20 + i % 35, percent, (ulong)(total * percent / 100), total, null, null, null, 0));
                }
                await Task.Delay(150); overlay.SaveSyntheticPreview(Path.ChangeExtension(output, ".png"));
            }
            using (var liveLifecycle = new MonitoringOverlay(this, false))
            {
                // No dispatcher yield while visible: exercise timer start/stop before a tick can run.
                liveLifecycle.Configure(new()); liveLifecycle.Show(); Check(liveLifecycle.Sampling, "visible non-test overlay starts its bounded timer");
                liveLifecycle.Hide(); Check(!liveLifecycle.Sampling, "hidden non-test overlay stops its timer");
                var none = new MetricViewPreferences { Numbers = false };
                liveLifecycle.Configure(new() { CpuView = none, MemoryView = none, NetworkView = none, DiskView = none, UptimeView = none });
                liveLifecycle.Show(); Check(!liveLifecycle.Sampling, "all view types off pauses sampling while metric switches remain independent"); liveLifecycle.Hide();
            }
            OverlayViewMetricChoice.SelectedIndex = 0;
            OverlayViewNumbers.IsChecked = true; OverlayViewCircle.IsChecked = false; OverlayViewCircleValue.IsChecked = true; OverlayViewBar.IsChecked = true;
            OverlayViewMetricChoice.SelectedIndex = 1; OverlayViewNumbers.IsChecked = false; OverlayViewCircle.IsChecked = true;
            OverlayViewMetricChoice.SelectedIndex = 0;
            var combined = OverlayChoices();
            Check(combined.CpuView is { Numbers: true, CircleWithValue: true, Bar: true } && combined.MemoryView is { Numbers: false, Circle: true } && combined.Cpu && combined.Memory, "view editor preserves combinations across metric selection without changing metric switches");
            PutPreferenceControls();
            var guide = new SetupGuidePreview(this);
            try { checks += guide.VerifyNavigationForChecks(); }
            finally { guide.Close(); }
            Check(preferences == initial && monitoringOverlay is null && !overlayHotkeyRegistered, "guide and synthetic overlay do not persist preferences or register shortcuts");
        }
        finally { preferences = initial; PutPreferenceControls(); ApplyPreferences(); }
        File.WriteAllText(output, $"{checks} overlay/guide UI and native window checks passed. Synthetic metrics only. No preferences saved, global shortcuts registered, maintenance, network requests or live monitoring performed.\n");
    }
}
