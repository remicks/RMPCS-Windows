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
    private readonly Queue<double> cpuHistory = new(), memoryHistory = new();
    private MonitorSample? previous;
    private OverlayPreferences options = new();
    private bool disposed, inFlight, moving, placing, queued;
    private int generation;
    private long sampleStarted;
    private HwndSource? source;
    internal bool Sampling => timer.IsEnabled;
    internal int HistoryCount => Math.Max(cpuHistory.Count, memoryHistory.Count);
    internal string? Error { get; private set; }
    internal event Action<string>? Issue;
    internal ScreenArea BoundsForChecks => Rectangle(this);
    internal long StyleForChecks => GetWindowLongPtrW(new WindowInteropHelper(this).Handle, -20).ToInt64();
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
            generation++; previous = null; cpuHistory.Clear(); memoryHistory.Clear(); ClearReadings();
            if (IsVisible && !disposed) { if (!testMode) timer.Start(); QueuePosition(); }
            else timer.Stop();
        };
        Closing += (_, e) => { if (!disposed) { e.Cancel = true; Hide(); } };
        ThemeManager.Applied += ApplyVisuals;
    }
    internal void Configure(OverlayPreferences next)
    {
        options = next.Normalize(); generation++; previous = null; cpuHistory.Clear(); memoryHistory.Clear(); ClearReadings();
        timer.Interval = TimeSpan.FromSeconds(options.IntervalSeconds);
        ApplyVisuals(); QueuePosition();
    }
    private void ApplyVisuals()
    {
        if (disposed) return;
        Opacity = SystemParameters.HighContrast ? 1 : options.OpacityPercent / 100d;
        FontSize = options.TextSize;
        if (options.Font == "App font") SetResourceReference(FontFamilyProperty, "UiFontFamily"); else FontFamily = FontManager.Family(options.Font);
        Topmost = options.Topmost; Width = options.Width;
        CpuValue.Visibility = options.Cpu ? Visibility.Visible : Visibility.Collapsed;
        MemoryValue.Visibility = options.Memory ? Visibility.Visible : Visibility.Collapsed;
        NetworkValue.Visibility = options.Network ? Visibility.Visible : Visibility.Collapsed;
        DiskValue.Visibility = options.DiskSpace ? Visibility.Visible : Visibility.Collapsed;
        UptimeValue.Visibility = options.Uptime ? Visibility.Visible : Visibility.Collapsed;
        CpuGraph.Visibility = options.Cpu && options.Graphs ? Visibility.Visible : Visibility.Collapsed;
        MemoryGraph.Visibility = options.Memory && options.Graphs ? Visibility.Visible : Visibility.Collapsed;
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
        if (disposed || !IsVisible || testMode) return;
        if (inFlight)
        {
            if (!Monitoring.Fresh(System.Diagnostics.Stopwatch.GetElapsedTime(sampleStarted).TotalSeconds, options.IntervalSeconds))
            { previous = null; cpuHistory.Clear(); memoryHistory.Clear(); ClearReadings("Waiting for Windows; earlier readings are stale."); }
            return;
        }
        inFlight = true; sampleStarted = System.Diagnostics.Stopwatch.GetTimestamp(); int ticket = generation; var selected = options;
        try
        {
            var sample = await Task.Run(() => probe.Read(selected));
            if (disposed || !IsVisible || ticket != generation) return;
            if (!Monitoring.Fresh(System.Diagnostics.Stopwatch.GetElapsedTime(sampleStarted).TotalSeconds, selected.IntervalSeconds))
            { previous = null; cpuHistory.Clear(); memoryHistory.Clear(); ClearReadings("Windows returned a stale sample; waiting for a fresh reading."); return; }
            Render(Monitoring.Read(previous, sample)); previous = sample;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception or NotSupportedException or ArgumentException or System.Security.SecurityException)
        { if (!disposed && ticket == generation) { previous = null; ClearReadings("Windows counters are unavailable. Retrying at the selected interval."); } }
        finally { inFlight = false; }
    }
    private void ClearReadings(string text = "Collecting local counters; — means unavailable or warming up.")
    {
        CpuValue.Text = "CPU —"; MemoryValue.Text = "RAM —"; NetworkValue.Text = "Network —";
        DiskValue.Text = "System drive free —"; UptimeValue.Text = "Uptime —"; SampleStatus.Text = text;
        CpuGraph.Points = new(); MemoryGraph.Points = new();
    }
    internal void Render(MonitorReading value)
    {
        CpuValue.Text = value.Cpu is double cpu ? $"CPU  {cpu:F0}%" : "CPU —";
        MemoryValue.Text = value.MemoryUsed is ulong used && value.MemoryTotal is ulong total ? $"RAM  {used / 1073741824d:F1} / {total / 1073741824d:F1} GiB ({value.MemoryPercent:F0}%)" : "RAM —";
        static string Rate(double? number) => number is double n && double.IsFinite(n) && n >= 0 ? n >= 1048576 ? $"{n / 1048576:F1} MiB/s" : $"{n / 1024:F1} KiB/s" : "—";
        NetworkValue.Text = $"Network  ↓ {Rate(value.DownloadBytes)}  ↑ {Rate(value.UploadBytes)}";
        DiskValue.Text = value.DiskFree is ulong free ? $"System drive free  {free / 1073741824d:F1} GiB" : "System drive free —";
        var uptime = TimeSpan.FromMilliseconds(value.UptimeMilliseconds);
        UptimeValue.Text = $"Uptime  {(int)uptime.TotalDays}d {uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
        SampleStatus.Text = $"Local · {options.IntervalSeconds}s updates · no data saved";
        Graph(CpuGraph, cpuHistory, value.Cpu); Graph(MemoryGraph, memoryHistory, value.MemoryPercent);
    }
    private void Graph(Polyline line, Queue<double> history, double? value)
    {
        if (!options.Graphs || value is not double number || !double.IsFinite(number)) { history.Clear(); line.Points = new(); return; }
        history.Enqueue(Math.Clamp(number, 0, 100)); while (history.Count > 60) history.Dequeue();
        var points = new PointCollection(); int i = 0; double width = Math.Max(1, ActualWidth - 32);
        foreach (double sample in history) points.Add(new Point(i++ * width / 59, 24 - .24 * sample));
        line.Points = points;
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
        ThemeManager.Applied -= ApplyVisuals; source?.RemoveHook(Hook); Close(); previous = null; cpuHistory.Clear(); memoryHistory.Clear();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr GetWindowLongPtrW(IntPtr window, int index);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] private static extern IntPtr SetWindowLongPtrW(IntPtr window, int index, IntPtr value);
}
