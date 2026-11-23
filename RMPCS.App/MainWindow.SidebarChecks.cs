using System.IO;
using System.Windows;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow
{
    internal async Task VerifySidebarUiAsync(string output)
    {
        if (!TestMode || sidebarDock is null) throw new InvalidOperationException("Sidebar test window unavailable.");
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        var original = preferences;
        var before = SidebarDock.Screens();
        // Some headless Windows runners expose a placeholder Shell_TrayWnd but
        // do not run Explorer's appbar service. Do not mistake that for a shell.
        bool shellAvailable = SidebarDock.ShellAvailable && System.Diagnostics.Process.GetProcessesByName("explorer").Length > 0;
        var originalBounds = sidebarDock.WindowArea;
        try
        {
            Check(!sidebarDock.Reserved, "Normal mode has no reservation");
            foreach (var screen in before)
            foreach (string edge in new[] { "Left", "Right" })
            {
                SaveSidebar(original with { SidebarEnabled = true, SidebarMonitor = screen.Device, SidebarEdge = edge });
                await Task.Delay(250); UpdateLayout();
                Check(sidebarVisual && MinWidth == 0 && ResizeMode == ResizeMode.NoResize && SidebarHeader.IsVisible, "Compact layout is active");
                Check(!sidebarDock.Reserved && SidebarDock.Screens().Single(s => s.Device == screen.Device).Work == screen.Work, "Overlay does not reserve work area");
                Check(!SidebarStatus.Text.Contains("is off"), "Entering or reentering sidebar mode refreshes its status");
                Check(sidebarDock.WindowArea.Top == screen.Work.Top && sidebarDock.WindowArea.Bottom == screen.Work.Bottom, "Sidebar respects taskbar height");
                Check(edge == "Left" ? sidebarDock.WindowArea.Left == screen.Work.Left : sidebarDock.WindowArea.Right == screen.Work.Right, "Selected monitor and edge are honored");
                for (int i = 0; i < MainTabs.Items.Count; i++)
                {
                    MainTabs.SelectedIndex = i; UpdateLayout();
                    Check(MainTabs.IsVisible && ResultsPanel.Visibility == Visibility.Collapsed && SidebarPageChoice.SelectedValue as string == AppPreferences.Pages[i], "Original pages are reachable in one pane");
                }
                ShowSidebarResults(); UpdateLayout();
                Check(ResultsPanel.IsVisible && MainTabs.Visibility == Visibility.Collapsed && RunButton.Visibility == Visibility.Collapsed, "Results occupy the pane without a hidden-page run action");
                SaveSidebar(preferences with { SidebarReserveSpace = true });
                await Task.Delay(350);
                var reserved = SidebarDock.Screens().Single(s => s.Device == screen.Device).Work;
                shellAvailable |= sidebarDock.Reserved;
                if (shellAvailable)
                {
                    Check(sidebarDock.Reserved && reserved.Width < screen.Work.Width && reserved.Height == screen.Work.Height, $"Windows reserves the chosen edge: registered={sidebarDock.Reserved}, before={screen.Work}, after={reserved}, window={sidebarDock.WindowArea}, status={SidebarStatus.Text}");
                    Check(edge == "Left" ? reserved.Left == sidebarDock.WindowArea.Right : reserved.Right == sidebarDock.WindowArea.Left, "Reserved work area matches sidebar width");
                }
                else Check(!sidebarDock.Reserved && reserved == screen.Work && SidebarStatus.Text.Contains("unavailable"), "Shell-less session safely reports overlay fallback");
                int positions = sidebarDock.PositionCount;
                await Task.Delay(450);
                Check(sidebarDock.PositionCount <= positions + 1, "Shell notifications do not create a reposition loop");
                Hide();
                Check(!sidebarDock.Reserved && SidebarDock.Screens().Single(s => s.Device == screen.Device).Work == screen.Work, "Hide releases reservation immediately");
                Show(); sidebarDock.Configure(preferences);
                Check(sidebarDock.Reserved == shellAvailable, "Reopening reacquires reservation when the shell is available");
                WindowState = WindowState.Minimized;
                Check(!sidebarDock.Reserved, "Minimizing releases reservation");
                WindowState = WindowState.Normal; sidebarDock.Configure(preferences);
                Check(sidebarDock.Reserved == shellAvailable, "Restore reacquires reservation when the shell is available");
                SaveSidebar(original); await Task.Delay(150);
                Check(!sidebarVisual && !sidebarDock.Reserved && SidebarDock.Screens().Single(s => s.Device == screen.Device).Work == screen.Work, "Full window restores desktop work area");
            }
            SaveSidebar(original with { SidebarEnabled = true, SidebarMonitor = @"\\.\DISPLAY999999", SidebarReserveSpace = true });
            await Task.Delay(200);
            Check(sidebarDock.Reserved == shellAvailable && SidebarStatus.Text.Contains("unavailable"), "Disconnected display falls back visibly");
            SaveSidebar(original); await Task.Delay(150);
            Check(sidebarDock.WindowArea == originalBounds || before.Any(s => sidebarDock.WindowArea.Width == Math.Min(originalBounds.Width, s.Work.Width) && sidebarDock.WindowArea.Height == Math.Min(originalBounds.Height, s.Work.Height)), "Normal window bounds are restored or fitted to an available display");
            SaveSidebar(original with { SidebarEnabled = true, SidebarReserveSpace = true });
            Check(sidebarDock.Reserved == shellAvailable, "Final disposal fixture follows shell availability");
            sidebarDock.Dispose();
            Check(!sidebarDock.Reserved && SidebarDock.Screens().SequenceEqual(before), "Disposal releases all owned screen space");
            VerifyIsolatedAppbar(Check);
            File.WriteAllText(output, $"{checks} sidebar UI/native placement and isolated shell lifecycle checks passed across {before.Count} display(s). Live shell reservation: {(shellAvailable ? "verified" : "unavailable in this session; overlay fallback verified")}. No preferences saved, maintenance run, or network requests made.\n");
        }
        finally { sidebarDock.Dispose(); }
    }

    private static void VerifyIsolatedAppbar(Action<bool, string> check)
    {
        var fixture = new Window { Width = 400, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = false };
        var baseline = SidebarDock.Screens();
        bool owned = false, failSet = false;
        int registrations = 0, removals = 0, sets = 0;
        string status = "";
        nuint Shell(uint message, ref SidebarDock.AppBarData data)
        {
            switch (message)
            {
                case 0: if (owned) throw new InvalidDataException("Duplicate appbar registration"); owned = true; registrations++; return 1;
                case 1: check(owned, "Only owned appbar registrations are removed"); owned = false; removals++; return 1;
                case 2: check(owned, "Position queries follow registration"); return 1;
                case 3: sets++; return failSet ? 0u : 1u;
                default: return 1;
            }
        }
        fixture.Show();
        using var dock = new SidebarDock(fixture, text => status = text, Shell);
        var options = new AppPreferences { SidebarEnabled = true, SidebarReserveSpace = true };
        try
        {
            dock.Configure(options);
            check(dock.Reserved && owned && registrations == 1 && sets == 1, "Successful shell registration precedes a reserved position");
            dock.Configure(options);
            check(registrations == 1 && sets == 1, "Repeated unchanged settings avoid duplicate registration and SETPOS feedback");
            dock.Configure(options with { SidebarEdge = "Left" });
            check(owned && registrations == 2 && removals == 1, "Changing sides releases the previous edge before reserving again");
            fixture.Hide(); check(!owned && !dock.Reserved, "Hide removes shell registration");
            fixture.Show(); dock.Configure(options); check(owned, "Visible restore reacquires shell registration");
            fixture.WindowState = WindowState.Minimized; check(!owned, "Minimize removes shell registration");
            fixture.WindowState = WindowState.Normal; dock.Configure(options); check(owned, "Normal restore reacquires shell registration");
            dock.Configure(options with { SidebarReserveSpace = false }); check(!owned, "Turning reservation off releases the work area");
            failSet = true; dock.Configure(options);
            check(!owned && !dock.Reserved && status.Contains("released"), "Failed SETPOS releases registration and reports failure");
            failSet = false; dock.Configure(options); check(owned, "Retry after a failed position can reserve again");
            check(status.Contains("reserved"), "Successful retry replaces the prior failure status");
            status = "Sidebar is off"; dock.Configure(options);
            check(status.Contains("reserved"), "Unchanged placement still refreshes a stale status label");
            dock.Dispose(); dock.Dispose();
            check(!owned && registrations == removals, "Disposal is idempotent and balances all appbar registrations");
            check(SidebarDock.Screens().SequenceEqual(baseline), "Isolated shell fixtures never change the real desktop work area");
        }
        finally { dock.Dispose(); fixture.Close(); }
    }
}
