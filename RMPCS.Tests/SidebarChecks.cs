using RMPCS.Core;

internal static class SidebarChecks
{
    public static void Run(Action<bool, string> check)
    {
        var defaults = AppPreferences.Parse("{}");
        check(!defaults.SidebarEnabled && !defaults.SidebarReserveSpace && defaults.SidebarMonitor == "Current", "sidebar and reservation are opt-in for existing and new preferences");
        check(SidebarLayout.ValidMonitor(@"\\.\DISPLAY1") && SidebarLayout.ValidMonitor("Current") && !SidebarLayout.ValidMonitor(@"\\.\DISPLAY") && !SidebarLayout.ValidMonitor("external.exe"), "monitor preferences accept only display identifiers or Current");
        var invalid = new AppPreferences { SidebarWidth = int.MaxValue, SidebarMonitor = null!, SidebarEdge = "Bottom" }.Normalize();
        check(invalid.SidebarWidth == 400 && invalid.SidebarMonitor == "Current" && invalid.SidebarEdge == "Right", "malformed sidebar data normalizes without executable settings");
        var enabled = new AppPreferences { SidebarEnabled = true, SidebarReserveSpace = true, SidebarEdge = "Left", SidebarWidth = 440, SidebarMonitor = @"\\.\DISPLAY12" };
        check(AppPreferences.Parse(enabled.ToJson()) == enabled && new PreferenceUndo(defaults, enabled).Restore(enabled) == defaults, "sidebar options round-trip and participate in preference undo");
        var area = new ScreenArea(-1920, -400, 0, 640);
        check(SidebarLayout.Dock(area, "Left", 400) == new ScreenArea(-1920, -400, -1520, 640) && SidebarLayout.Dock(area, "Right", 400) == new ScreenArea(-400, -400, 0, 640), "both edges preserve negative monitor origins and taskbar-adjusted vertical space");
        check(SidebarLayout.PixelWidth(400, 144, 2560) == 600 && SidebarLayout.PixelWidth(480, 192, 1280) == 640, "width follows monitor DPI and reserves at most half the display");
        check(SidebarLayout.PixelWidth(-1, 0, 1920) == 400 && SidebarLayout.PixelWidth(480, 768, 1) == 1, "invalid DPI and tiny screens produce bounded geometry");
        check(SidebarLayout.Dock(new(0, 0, 200, 300), "Left", 400).Width == 200, "docking never extends beyond the available horizontal area");
        bool rejected = false; try { SidebarLayout.Dock(new(0, 0, 0, 100), "Right", 400); } catch (ArgumentException) { rejected = true; }
        check(rejected, "invalid monitor geometry is rejected before native positioning");
    }
}
