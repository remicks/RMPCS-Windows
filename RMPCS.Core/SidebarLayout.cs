namespace RMPCS.Core;

// Coordinates here are physical screen pixels, including negative monitor origins.
public readonly record struct ScreenArea(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public static class SidebarLayout
{
    public static bool ValidMonitor(string? value) => value == "Current" ||
        (value is { Length: > 11 and <= 24 } && value.StartsWith(@"\\.\DISPLAY", StringComparison.Ordinal) &&
         value[11..].All(c => c is >= '0' and <= '9'));

    public static int PixelWidth(int logicalWidth, uint dpi, int availableWidth)
    {
        if (availableWidth <= 0) throw new ArgumentOutOfRangeException(nameof(availableWidth));
        int width = logicalWidth is 360 or 400 or 440 or 480 ? logicalWidth : 400;
        double scale = dpi is >= 96 and <= 768 ? dpi / 96.0 : 1;
        // Leave at least half the monitor available to other applications.
        return Math.Clamp((int)Math.Round(width * scale), 1, Math.Max(1, availableWidth / 2));
    }

    public static ScreenArea Dock(ScreenArea area, string edge, int width)
    {
        if (area.Width <= 0 || area.Height <= 0) throw new ArgumentException("No usable monitor area.");
        width = Math.Clamp(width, 1, area.Width);
        return edge == "Left" ? area with { Right = area.Left + width } : area with { Left = area.Right - width };
    }
}
