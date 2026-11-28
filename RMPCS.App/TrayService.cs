using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace RMPCS.App;

// WPF-owned notification icon: no background maintenance or command execution.
internal sealed class TrayService : IDisposable
{
    private readonly HwndSource source;
    private readonly Action show, exit;
    private readonly Action? toggleOverlay;
    private readonly Action<RMPCS.Core.CompletionNotice>? completion;
    private readonly uint taskbarCreated;
    private IntPtr icon;
    private bool added, disposed;
    private const int Callback = 0x8001;
    public bool Available => added && !disposed;
    public string Diagnostic { get; private set; } = "Not initialized";
    public TrayService(Action show, Action exit, Action<RMPCS.Core.CompletionNotice>? completion = null, Action? toggleOverlay = null)
    {
        this.show = show; this.exit = exit;
        this.toggleOverlay = toggleOverlay;
        this.completion = completion;
        source = new HwndSource(new HwndSourceParameters(CompletionRelay.CurrentCaption) { Width = 0, Height = 0, WindowStyle = 0 });
        source.AddHook(Message);
        taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        icon = LoadImage(IntPtr.Zero, System.IO.Path.Combine(AppContext.BaseDirectory, "RMPCS.ico"), 1, 32, 32, 0x10);
        Diagnostic = "Icon handle created: " + (icon != IntPtr.Zero) + "; Win32 error: " + Marshal.GetLastWin32Error();
        if (icon != IntPtr.Zero) Add();
    }
    private NotifyData Data() => new() { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = source.Handle, Id = 1,
        Flags = 1 | 2 | 4, CallbackMessage = Callback, Icon = icon, Tip = "Rusty's PC Toolkit • Ready", Info = "", InfoTitle = "" };
    private void Add() { var data = Data(); added = ShellNotifyIcon(0, ref data); Diagnostic = "Tray registered: " + added + "; Win32 error: " + Marshal.GetLastWin32Error(); }
    public void Status(bool busy)
    {
        if (!Available) return;
        var data = Data(); data.Tip = busy ? "Rusty's PC Toolkit • Task running" : "Rusty's PC Toolkit • Ready";
        ShellNotifyIcon(1, ref data);
    }
    public void Notify(string text)
    {
        if (!Available) return;
        var data = Data(); data.Flags = 0x10; data.InfoTitle = "Rusty's PC Toolkit"; data.Info = text; data.InfoFlags = 1;
        ShellNotifyIcon(1, ref data);
    }
    private IntPtr Message(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == CompletionRelay.Message)
        {
            long value = wParam.ToInt64();
            if (lParam.ToInt64() == CompletionRelay.Marker && value is >= 1 and <= 6 && Enum.IsDefined((RMPCS.Core.CompletionNotice)(int)value)) completion?.Invoke((RMPCS.Core.CompletionNotice)(int)value);
            handled = true; return IntPtr.Zero;
        }
        if ((uint)message == taskbarCreated) { if (!disposed && icon != IntPtr.Zero) { Add(); if (!Available) show(); } }
        if (message == Callback)
        {
            int mouse = unchecked((int)lParam.ToInt64());
            if (mouse is 0x202 or 0x203) show(); // Left click / double click.
            else if (mouse == 0x205)
            {
                var menu = new ContextMenu();
                var open = new MenuItem { Header = "Open RMPCS" }; open.Click += (_, _) => show();
                var quit = new MenuItem { Header = "Exit RMPCS" }; quit.Click += (_, _) => exit();
                menu.Items.Add(open);
                if (toggleOverlay is not null) { var overlay = new MenuItem { Header = "Show / hide system overlay" }; overlay.Click += (_, _) => toggleOverlay(); menu.Items.Add(overlay); }
                menu.Items.Add(new Separator()); menu.Items.Add(quit);
                SetForegroundWindow(source.Handle);
                menu.Closed += (_, _) => PostMessage(source.Handle, 0, IntPtr.Zero, IntPtr.Zero);
                menu.IsOpen = true;
            }
            handled = true;
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (disposed) return;
        if (added) { var data = Data(); ShellNotifyIcon(2, ref data); }
        disposed = true; added = false; source.Dispose();
        if (icon != IntPtr.Zero) { DestroyIcon(icon); icon = IntPtr.Zero; }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, CallbackMessage; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyData data);
    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
