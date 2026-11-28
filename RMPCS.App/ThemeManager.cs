using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using RMPCS.Core;
namespace RMPCS.App;
internal static class ThemeManager
{
    private static AppPreferences current = new();
    private static bool listening;
    internal static event Action? Applied;
    public static bool Dark { get; private set; }
    public static void Initialize()
    {
        Apply(new()); if(listening)return; listening=true;
        SystemEvents.UserPreferenceChanged += WindowsPreferenceChanged;
        SystemParameters.StaticPropertyChanged += SystemParameterChanged;
    }
    public static void Dispose() { if(!listening)return;SystemEvents.UserPreferenceChanged-=WindowsPreferenceChanged;SystemParameters.StaticPropertyChanged-=SystemParameterChanged;listening=false; }
    private static void WindowsPreferenceChanged(object sender,UserPreferenceChangedEventArgs e) => Reapply();
    private static void SystemParameterChanged(object? sender,PropertyChangedEventArgs e) { if(e.PropertyName==nameof(SystemParameters.HighContrast))Reapply(); }
    private static void Reapply() { var app=Application.Current; if(app is not null&&!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke(()=>Apply(current)); }
    private static bool WindowsDark()
    {
        try { using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("AppsUseLightTheme") is int value&&value==0; }catch{return false;}
    }
    public static void Apply(AppPreferences preferences)
    {
        current=preferences.Normalize(); bool highContrast=SystemParameters.HighContrast;
        FontManager.Apply(current);
        Dark=current.ThemeMode=="Dark"||(current.ThemeMode=="System"&&WindowsDark());
        string accent=ThemeCatalog.Accent(current.AccentName,current.CustomAccent);
        void Set(string key,string value) { var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));brush.Freeze();Application.Current.Resources[key]=brush; }
        var palette=ThemeCatalog.Surfaces(current.AccentName,Dark);
        string surface=palette.Surface;
        Set("WindowBrush",palette.Window);Set("SurfaceBrush",surface);Set("RaisedBrush",palette.Raised);
        Set("SubtleBorderBrush",palette.SubtleBorder);
        Set("SelectedTextBrush",palette.Text);Set("SwitchThumbBrush","#FFFFFF");
        Set("TextBrush",palette.Text);Set("MutedBrush",palette.Muted);Set("BorderBrush",palette.Border);
        Set("HoverBrush",palette.Hover);Set("SelectionBrush",palette.Selection);Set("SwitchOffBrush",palette.SwitchOff);
        Set("AccentBrush",accent);Set("AccentTextBrush",ThemeCatalog.ReadableAccent(accent,surface));Set("OnAccentBrush",ThemeCatalog.OnAccent(accent));
        if(highContrast)
        {
            void System(string key,Color color){var brush=new SolidColorBrush(color);brush.Freeze();Application.Current.Resources[key]=brush;}
            foreach(string key in new[]{"WindowBrush","SurfaceBrush","RaisedBrush"})System(key,SystemColors.WindowColor);
            foreach(string key in new[]{"TextBrush","MutedBrush","AccentTextBrush"})System(key,SystemColors.WindowTextColor);
            System("BorderBrush",SystemColors.WindowTextColor);System("SwitchOffBrush",SystemColors.WindowTextColor);
            System("SubtleBorderBrush",SystemColors.WindowTextColor);
            foreach(string key in new[]{"AccentBrush","SelectionBrush","HoverBrush"})System(key,SystemColors.HighlightColor);
            System("OnAccentBrush",SystemColors.HighlightTextColor);System("SelectedTextBrush",SystemColors.HighlightTextColor);System("SwitchThumbBrush",SystemColors.WindowColor);
        }
        foreach(Window window in Application.Current.Windows) ApplyTitleBar(window);
        Applied?.Invoke();
    }
    public static void ApplyTitleBar(Window window)
    {
        try { var handle=new WindowInteropHelper(window).Handle; if(handle==IntPtr.Zero)return;int dark=Dark&&!SystemParameters.HighContrast?1:0;DwmSetWindowAttribute(handle,20,ref dark,sizeof(int)); }catch{ }
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
