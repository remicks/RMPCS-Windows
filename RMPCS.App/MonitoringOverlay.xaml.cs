using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using RMPCS.Core;

namespace RMPCS.App;

internal partial class MonitoringOverlay : Window, IDisposable
{
    private readonly Window main;
    private readonly bool testMode;
    private readonly MonitorProbe probe = new();
    private readonly DispatcherTimer timer;
    private MonitorSample? previous;
    private OverlayPreferences options = new OverlayPreferences().Normalize();
    private bool disposed, inFlight, moving, placing, queued;
    private int generation;
    private long sampleStarted;
    private HwndSource? source;
    internal bool Sampling => timer.IsEnabled;
    private MetricDisplay[] Displays => [CpuDisplay, MemoryDisplay, DownloadDisplay, UploadDisplay, DiskDisplay, UptimeDisplay];
    internal int HistoryCount => Displays.Max(d => d.HistoryCount);
    internal MetricDisplay[] DisplaysForChecks => Displays;
    internal string? Error { get; private set; }
    internal event Action<string>? Issue;
    internal ScreenArea BoundsForChecks => Rectangle(this);
    internal long StyleForChecks => GetWindowLongPtrW(new WindowInteropHelper(this).Handle, -20).ToInt64();
    internal void SaveSyntheticPreview(string path)
    {
        if (!testMode) throw new InvalidOperationException("Preview export requires synthetic test mode.");
        UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(ActualWidth)), Math.Max(1, (int)Math.Ceiling(ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None); encoder.Save(stream);
    }
    internal MonitoringOverlay(Window main, bool testMode)
    {
        this.main = main; this.testMode = testMode;
        InitializeComponent();
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += Tick;
        SourceInitialized += (_, _) => { source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source?.AddHook(Hook); ApplyVisuals(); };
        SizeChanged += (_, _) => QueuePosition();
        IsVisibleChanged += (_, _) =>
        {
            generation++; previous = null; ClearReadings();
            if (IsVisible && !disposed) { if (!testMode && options.HasVisibleMetrics()) timer.Start(); QueuePosition(); }
            else timer.Stop();
        };
        Closing += (_, e) => { if (!disposed) { e.Cancel = true; Hide(); } };
        ThemeManager.Applied += ApplyVisuals;
    }
    internal void Configure(OverlayPreferences next)
    {
        options = next.Normalize(); generation++; previous = null; ClearReadings();
        timer.Interval = TimeSpan.FromSeconds(options.IntervalSeconds);
        ApplyVisuals(); QueuePosition();
        if (IsVisible && !testMode && options.HasVisibleMetrics()) timer.Start(); else timer.Stop();
    }
    private void ApplyVisuals()
    {
        if (disposed) return;
        Opacity = SystemParameters.HighContrast ? 1 : options.OpacityPercent / 100d;
        FontSize = options.TextSize;
        if (options.Font == "App font") SetResourceReference(FontFamilyProperty, "UiFontFamily"); else FontFamily = FontManager.Family(options.Font);
        Topmost = options.Topmost; Width = options.Width;
        CpuDisplay.Configure("CPU", "Usage · 0–100%", options.Cpu, options.CpuView!);
        MemoryDisplay.Configure("RAM", "Used memory · 0–100%", options.Memory, options.MemoryView!);
        string networkScale = $"Reference {options.NetworkScaleMiB} MiB/s · not link capacity";
        DownloadDisplay.Configure("NETWORK DOWNLOAD", networkScale, options.Network, options.NetworkView!);
        UploadDisplay.Configure("NETWORK UPLOAD", networkScale, options.Network, options.NetworkView!);
        DiskDisplay.Configure("SYSTEM DRIVE AVAILABLE", "Available space · % of total capacity", options.DiskSpace, options.DiskView!);
        UptimeDisplay.Configure("WINDOWS UPTIME", "Current 24-hour uptime cycle", options.Uptime, options.UptimeView!);
        NoViews.Visibility = options.HasVisibleMetrics() ? Visibility.Collapsed : Visibility.Visible;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            long style = GetWindowLongPtrW(hwnd, -20).ToInt64();
            style = (style | 0x08000080L) & ~0x20L; // NOACTIVATE | TOOLWINDOW; explicit click-through opt-in
            if (options.ClickThrough) style |= 0x20L;
            Marshal.SetLastPInvokeError(0);
            if (SetWindowLongPtrW(hwnd, -20, new IntPtr(style)) == IntPtr.Zero && Marshal.GetLastWin32Error() != 0 || !SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x37))
            { Error = "Overlay window controls are unavailable. The overlay has been hidden; use Settings to retry."; Hide(); }
            else Error = null;
        }
    }
    private async void Tick(object? sender, EventArgs e)
    {
        if (disposed || !IsVisible || testMode || !options.HasVisibleMetrics()) return;
        if (inFlight)
        {
            if (!Monitoring.Fresh(System.Diagnostics.Stopwatch.GetElapsedTime(sampleStarted).TotalSeconds, options.IntervalSeconds))
            { previous = null; ClearReadings("Waiting for Windows; earlier readings are stale."); }
            return;
        }
        inFlight = true; sampleStarted = System.Diagnostics.Stopwatch.GetTimestamp(); int ticket = generation; var selected = options;
        try
        {
            var sample = await Task.Run(() => probe.Read(selected.SamplingChoices()));
            if (disposed || !IsVisible || ticket != generation) return;
            if (!Monitoring.Fresh(System.Diagnostics.Stopwatch.GetElapsedTime(sampleStarted).TotalSeconds, selected.IntervalSeconds))
            { previous = null; ClearReadings("Windows returned a stale sample; waiting for a fresh reading."); return; }
            Render(Monitoring.Read(previous, sample)); previous = sample;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception or NotSupportedException or ArgumentException or System.Security.SecurityException)
        { if (!disposed && ticket == generation) { previous = null; ClearReadings("Windows counters are unavailable. Retrying at the selected interval."); } }
        finally { inFlight = false; }
    }
    private void ClearReadings(string text = "Collecting local counters; — means unavailable or warming up.")
    {
        foreach (var display in Displays) display.Clear();
        SampleStatus.Text = options.HasVisibleMetrics() ? text : "Sampling paused · no visible metric views";
    }
    internal void Render(MonitorReading value)
    {
        static string Percent(double? number) => number is double n && double.IsFinite(n) ? $"{n:F0}%" : "—";
        CpuDisplay.Update(Percent(value.Cpu), Percent(value.Cpu), value.Cpu);
        MemoryDisplay.Update(value.MemoryUsed is ulong used && value.MemoryTotal is ulong total ? $"{used / 1073741824d:F1} / {total / 1073741824d:F1} GiB ({value.MemoryPercent:F0}%)" : "—", Percent(value.MemoryPercent), value.MemoryPercent);
        static string Rate(double? number) => number is double n && double.IsFinite(n) && n >= 0 ? n >= 1048576 ? $"{n / 1048576:F1} MiB/s" : $"{n / 1024:F1} KiB/s" : "—";
        DownloadDisplay.Update(Rate(value.DownloadBytes), Rate(value.DownloadBytes).Replace(" ", "\n"), Monitoring.RelativePercent(value.DownloadBytes, options.NetworkScaleMiB * 1048576d));
        UploadDisplay.Update(Rate(value.UploadBytes), Rate(value.UploadBytes).Replace(" ", "\n"), Monitoring.RelativePercent(value.UploadBytes, options.NetworkScaleMiB * 1048576d));
        DiskDisplay.Update(value.DiskFree is ulong free ? $"{free / 1073741824d:F1} GiB available" : "—", Percent(value.DiskAvailablePercent), value.DiskAvailablePercent);
        var uptime = TimeSpan.FromMilliseconds(Math.Clamp(value.UptimeMilliseconds, 0, TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond));
        UptimeDisplay.Update($"{(int)uptime.TotalDays}d {uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}", $"{(int)uptime.TotalDays}d\n{uptime.Hours:00}:{uptime.Minutes:00}", uptime.TotalMilliseconds % 86400000 / 86400000d * 100);
        SampleStatus.Text = $"Local · {options.IntervalSeconds}s updates · no data saved";
    }
    private void Hide_Click(object sender, RoutedEventArgs e) { e.Handled = true; Hide(); }
    private void Frame_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (options.Locked || options.ClickThrough || e.OriginalSource is System.Windows.Controls.Button) return;
        moving = true;
        try { DragMove(); options = CapturePosition(); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or ArgumentException) { }
        finally { moving = false; QueuePosition(); }
    }
    internal OverlayPreferences CapturePosition()
    {
        var screens = SidebarDock.Screens(); var rect = Rectangle(this);
        var screen = screens.OrderByDescending(s => Overlap(s.Work, rect)).FirstOrDefault();
        if (screen.Work.Width <= 0 || screen.Work.Height <= 0) return options;
        return (options with { Monitor = screen.Device, Position = "Custom",
            X = (rect.Left - screen.Work.Left) / (double)Math.Max(1, screen.Work.Width - rect.Width),
            Y = (rect.Top - screen.Work.Top) / (double)Math.Max(1, screen.Work.Height - rect.Height) }).Normalize();
    }
    private void QueuePosition()
    {
        if (disposed || queued || placing || moving || !IsVisible) return;
        queued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            queued = false;
            try { Place(); }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or ArgumentException)
            { Error = "Overlay placement is unavailable. It has been hidden; use Settings to retry."; Hide(); Issue?.Invoke(Error); }
        }));
    }
    private void Place()
    {
        if (disposed || !IsVisible || placing || moving) return;
        placing = true;
        try
        {
            var screens = SidebarDock.Screens(); if (screens.Count == 0) throw new InvalidOperationException("No monitor available.");
            var owner = Rectangle(main);
            var screen = screens.FirstOrDefault(s => s.Device == options.Monitor);
            if (screen.Work.Width <= 0) screen = screens.OrderByDescending(s => Overlap(s.Work, owner)).First();
            var scale = VisualTreeHelper.GetDpi(this);
            MaxWidth = Math.Max(1, screen.Work.Width / scale.DpiScaleX); MaxHeight = Math.Max(1, screen.Work.Height / scale.DpiScaleY);
            MetricsScroll.MaxHeight = Math.Max(1, MaxHeight - 100);
            Width = Math.Min(options.Width, MaxWidth); UpdateLayout();
            var current = Rectangle(this);
            var target = Monitoring.Place(screen.Work, current.Width, current.Height, options);
            if (!SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(options.Topmost ? -1 : -2), target.Left, target.Top, target.Width, target.Height, 0x10)) throw new Win32Exception();
        }
        finally { placing = false; }
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is 0x007E or 0x02E0 or 0x001A) QueuePosition(); // display / DPI / work-area change
        if (message == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE, never steal keyboard focus
        return IntPtr.Zero;
    }
    private static long Overlap(ScreenArea a, ScreenArea b) => (long)Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left)) * Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
    private static ScreenArea Rectangle(Window window) => GetWindowRect(new WindowInteropHelper(window).Handle, out var rect) ? new(rect.Left, rect.Top, rect.Right, rect.Bottom) : new(0, 0, 1, 1);
    public void Dispose()
    {
        if (disposed) return; disposed = true; generation++; timer.Stop(); timer.Tick -= Tick;
        ThemeManager.Applied -= ApplyVisuals; source?.RemoveHook(Hook); Close(); previous = null; foreach (var display in Displays) display.Clear();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr GetWindowLongPtrW(IntPtr window, int index);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] private static extern IntPtr SetWindowLongPtrW(IntPtr window, int index, IntPtr value);
}
