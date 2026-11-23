using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using RMPCS.Core;

namespace RMPCS.App;

internal sealed record MonitorChoice(string Key, string Label)
{
    public override string ToString() => Label;
}

// Owns only this HWND's appbar registration. Never writes the global work area,
// taskbar settings, other windows' bounds, or a persistent shell setting.
internal sealed class SidebarDock : IDisposable
{
    private readonly Window window;
    private readonly HwndSource source;
    private readonly Action<string> report;
    private readonly DispatcherTimer timer;
    private readonly nint hwnd;
    internal delegate nuint ShellMessage(uint message, ref AppBarData data);
    private readonly ShellMessage shell;
    private readonly uint callback = RegisterWindowMessage("DegeneratesInc.RMPCS.Sidebar");
    private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private AppPreferences options = new();
    private bool registered, positioning, disposed, fullscreen, haveRestore;
    private NativeRect restore;
    private ScreenArea? reservation;
    private ScreenArea? lastPlaced;
    private WindowState restoreState;
    private bool restoreTopmost;
    private string? currentMonitor;
    private string? lastReport;
    internal bool Reserved => registered;
    internal int PositionCount { get; private set; }
    internal ScreenArea WindowArea => GetWindowRect(hwnd, out var rect) ? rect.Area : throw new Win32Exception();
    internal static IReadOnlyList<(string Device, ScreenArea Bounds, ScreenArea Work)> Screens() => Monitors().Select(m => (m.Device, m.Bounds.Area, m.Work.Area)).ToArray();

    internal SidebarDock(Window window, Action<string> report, ShellMessage? shell = null)
    {
        this.window = window; this.report = report;
        this.shell = shell ?? SHAppBarMessage;
        hwnd = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("Window handle unavailable.");
        if (callback == 0 || taskbarCreated == 0) throw new Win32Exception();
        // Explorer normally runs unelevated. These two messages carry only shell
        // layout hints; they never authorize maintenance, execute data, or open paths.
        ChangeWindowMessageFilterEx(hwnd, callback, 1, 0);
        ChangeWindowMessageFilterEx(hwnd, taskbarCreated, 1, 0);
        timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) => { timer.Stop(); Position(); };
        source.AddHook(Hook);
        window.IsVisibleChanged += VisibilityChanged;
        window.StateChanged += StateChanged;
    }

    internal static IReadOnlyList<MonitorChoice> Choices() => new[] { new MonitorChoice("Current", "Current monitor (when sidebar opens)") }
        .Concat(Monitors().Select(m => new MonitorChoice(m.Device, $"{m.Device.Replace(@"\\.\", "")} · {m.Bounds.Width} × {m.Bounds.Height}{(m.Primary ? " · Main display" : "")}"))).ToArray();

    internal void Configure(AppPreferences next)
    {
        if (disposed) return;
        next = next.Normalize();
        bool entering = next.SidebarEnabled && !options.SidebarEnabled;
        bool changeLocation = options.SidebarEdge != next.SidebarEdge || options.SidebarMonitor != next.SidebarMonitor || options.SidebarReserveSpace != next.SidebarReserveSpace;
        if (entering)
        {
            CaptureRestore();
        }
        if (changeLocation) Release();
        options = next;
        if (!next.SidebarEnabled)
        {
            timer.Stop(); Release(); fullscreen = false; window.Topmost = restoreTopmost;
            if (haveRestore)
            {
                window.WindowState = WindowState.Normal;
                var monitors = Monitors();
                var area = monitors.FirstOrDefault(m => m.Bounds.Intersects(restore)) ?? monitors.FirstOrDefault();
                if (area is not null)
                {
                    int w = Math.Min(restore.Width, area.Work.Width), h = Math.Min(restore.Height, area.Work.Height);
                    int x = Math.Clamp(restore.Left, area.Work.Left, area.Work.Right - w), y = Math.Clamp(restore.Top, area.Work.Top, area.Work.Bottom - h);
                    SetWindowPos(hwnd, 0, x, y, w, h, 0x0014); // NOZORDER | NOACTIVATE
                }
                window.WindowState = restoreState == WindowState.Minimized ? WindowState.Normal : restoreState;
                haveRestore = false;
            }
            return;
        }
        if (window.IsVisible && window.WindowState != WindowState.Minimized) Position();
    }

    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!window.IsVisible) { timer.Stop(); Release(); }
        else Queue();
    }
    private void StateChanged(object? sender, EventArgs e)
    {
        if (window.WindowState == WindowState.Minimized) { timer.Stop(); Release(); }
        else Queue();
    }
    private void Queue()
    {
        if (!disposed && options.SidebarEnabled && window.IsVisible && window.WindowState != WindowState.Minimized)
        { timer.Stop(); timer.Start(); }
    }
    private void Position()
    {
        if (disposed || positioning || !options.SidebarEnabled || !window.IsVisible || window.WindowState == WindowState.Minimized) return;
        positioning = true;
        PositionCount++;
        try
        {
            if (window.WindowState == WindowState.Maximized) window.WindowState = WindowState.Normal;
            var monitors = Monitors();
            string? selected = options.SidebarMonitor == "Current" ? currentMonitor : options.SidebarMonitor;
            var monitor = monitors.FirstOrDefault(m => m.Device == selected);
            bool missing = monitor is null;
            monitor ??= MonitorForWindow() ?? monitors.FirstOrDefault(m => m.Primary) ?? monitors.FirstOrDefault();
            if (monitor is null) throw new InvalidOperationException("No display available.");
            if (options.SidebarMonitor == "Current") currentMonitor = monitor.Device;
            // Move to the target before asking GetDpiForWindow, so Windows supplies
            // that monitor's effective DPI. All shell rectangles stay in pixels.
            if (MonitorForWindow()?.Device != monitor.Device)
                Move(new ScreenArea(monitor.Work.Left, monitor.Work.Top, monitor.Work.Left + Math.Min(400, monitor.Work.Width), monitor.Work.Top + monitor.Work.Height));
            uint dpi = GetDpiForWindow(hwnd);
            int width = SidebarLayout.PixelWidth(options.SidebarWidth, dpi, monitor.Bounds.Width);
            var rect = SidebarLayout.Dock(monitor.Work.Area, options.SidebarEdge, width);
            bool reserveFailed = false;
            if (options.SidebarReserveSpace)
            {
                if (!registered)
                {
                    var registration = Data(); registration.CallbackMessage = callback;
                    registered = shell(0, ref registration) != 0; // ABM_NEW
                }
                if (registered)
                {
                    var data = Data(); data.Edge = options.SidebarEdge == "Left" ? 0u : 2u;
                    data.Rect = NativeRect.From(SidebarLayout.Dock(monitor.Bounds.Area, options.SidebarEdge, width));
                    shell(2, ref data); // ABM_QUERYPOS excludes other bars, including the taskbar.
                    data.Rect = NativeRect.From(SidebarLayout.Dock(data.Rect.Area, options.SidebarEdge, width));
                    if (reservation != data.Rect.Area)
                    {
                        if (shell(3, ref data) == 0) throw new InvalidOperationException("Windows refused the appbar position.");
                        reservation = data.Rect.Area;
                    }
                    rect = reservation.Value;
                }
                else reserveFailed = true;
            }
            else Release();
            Move(rect);
            window.Topmost = !fullscreen;
            string status = (missing && options.SidebarMonitor != "Current" ? "Selected display unavailable; using an available display. " : "") +
                (reserveFailed ? "Space reservation unavailable. Sidebar is overlaying the desktop; try applying again." : registered ? "Desktop space reserved while visible." : "Overlay mode · desktop space is not reserved.");
            if (lastReport != status) { lastReport = status; report(status); }
        }
        catch
        {
            Release(); report("Sidebar docking could not complete. Space reservation was released. Use Full window or apply sidebar settings again.");
        }
        finally { positioning = false; }
    }
    private void Move(ScreenArea r)
    {
        if (!GetWindowRect(hwnd, out var current) || current.Area != r)
            if (!SetWindowPos(hwnd, 0, r.Left, r.Top, r.Width, r.Height, 0x0014)) throw new Win32Exception();
        lastPlaced = r;
    }
    private AppBarData Data() => new() { Size = (uint)Marshal.SizeOf<AppBarData>(), Window = hwnd };
    internal void CaptureRestore()
    {
        if (haveRestore) return;
        haveRestore = GetWindowRect(hwnd, out restore);
        restoreState = window.WindowState; restoreTopmost = window.Topmost;
        currentMonitor = MonitorForWindow()?.Device;
    }
    private void Release()
    {
        if (!registered) return;
        registered = false;
        reservation = null;
        fullscreen = false;
        var data = Data(); shell(1, ref data); // ABM_REMOVE before hiding or destroying this HWND.
    }
    private nint Hook(nint h, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (disposed) return 0;
        if ((uint)message == taskbarCreated)
        {
            // Explorer lost its registrations. Re-register this visible window only.
            // Removing our own HWND first is harmless after an actual restart and
            // preserves ownership bookkeeping even if this layout hint is spurious.
            Release(); reservation = null; Queue();
        }
        if ((uint)message == callback && registered)
        {
            if (wParam == 1 && !positioning) Queue(); // ABN_POSCHANGED; ignore notifications generated by our own SETPOS.
            if (wParam == 2) // ABN_FULLSCREENAPP: respect exclusive/full-screen applications.
            {
                fullscreen = lParam != 0; window.Topmost = !fullscreen;
                if (fullscreen) SetWindowPos(hwnd, 1, 0, 0, 0, 0, 0x0013); // HWND_BOTTOM, no move/size/activate.
            }
        }
        if (message is 0x007E or 0x02E0 || (message == 0x001A && !positioning)) Queue(); // display/DPI/work-area changes.
        if (options.SidebarEnabled && window.IsVisible)
        {
            if (message == 0x0112 && ((wParam.ToInt64() & 0xFFF0) is 0xF000 or 0xF010 or 0xF030)) handled = true; // size/move/maximize
            if (registered && message == 0x0006) { var d = Data(); d.Param = (wParam.ToInt64() & 0xFFFF) != 0 ? 1 : 0; shell(6, ref d); }
            if (registered && message == 0x0047) { var d = Data(); shell(9, ref d); } // WINDOWPOSCHANGED
            if (message == 0x0047 && !positioning && lastPlaced is { } expected && GetWindowRect(hwnd, out var actual) && actual.Area != expected) Queue();
        }
        return 0;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; timer.Stop(); Release();
        window.IsVisibleChanged -= VisibilityChanged; window.StateChanged -= StateChanged;
        source.RemoveHook(Hook);
    }

    private sealed record Monitor(string Device, NativeRect Bounds, NativeRect Work, bool Primary);
    private static Monitor? MonitorForWindow(nint handle) => ReadMonitor(MonitorFromWindow(handle, 2));
    private Monitor? MonitorForWindow() => MonitorForWindow(hwnd);
    private static IReadOnlyList<Monitor> Monitors()
    {
        var result = new List<Monitor>();
        MonitorEnum callback = (nint monitor, nint dc, ref NativeRect rect, nint data) => { if (ReadMonitor(monitor) is { } item) result.Add(item); return true; };
        if (!EnumDisplayMonitors(0, 0, callback, 0)) throw new Win32Exception();
        return result;
    }
    private static Monitor? ReadMonitor(nint handle)
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>(), Device = "" };
        return GetMonitorInfo(handle, ref info) ? new(info.Device, info.Bounds, info.Work, (info.Flags & 1) != 0) : null;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
        public readonly ScreenArea Area => new(Left, Top, Right, Bottom);
        public readonly bool Intersects(NativeRect other) => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
        public static NativeRect From(ScreenArea r) => new() { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom };
    }
    [StructLayout(LayoutKind.Sequential)] internal struct AppBarData { public uint Size; public nint Window; public uint CallbackMessage, Edge; public NativeRect Rect; public nint Param; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo { public uint Size; public NativeRect Bounds, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    private delegate bool MonitorEnum(nint monitor, nint dc, ref NativeRect rect, nint data);
    [DllImport("shell32.dll")] private static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string value);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnum callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    internal static bool ShellAvailable => FindWindow("Shell_TrayWnd", null) != 0;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? windowName);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ChangeWindowMessageFilterEx(nint window, uint message, uint action, nint info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
