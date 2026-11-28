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
            PutPreferenceControls(); var retained = preferences.Overlay;
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
            }
            using (var liveLifecycle = new MonitoringOverlay(this, false))
            {
                // No dispatcher yield while visible: exercise timer start/stop before a tick can run.
                liveLifecycle.Configure(new()); liveLifecycle.Show(); Check(liveLifecycle.Sampling, "visible non-test overlay starts its bounded timer");
                liveLifecycle.Hide(); Check(!liveLifecycle.Sampling, "hidden non-test overlay stops its timer");
            }
            var guide = new SetupGuidePreview(this);
            try { checks += guide.VerifyNavigationForChecks(); }
            finally { guide.Close(); }
            Check(preferences == initial && monitoringOverlay is null && !overlayHotkeyRegistered, "guide and synthetic overlay do not persist preferences or register shortcuts");
        }
        finally { preferences = initial; PutPreferenceControls(); ApplyPreferences(); }
        File.WriteAllText(output, $"{checks} overlay/guide UI and native window checks passed. Synthetic metrics only. No preferences saved, global shortcuts registered, maintenance, network requests or live monitoring performed.\n");
    }
}
