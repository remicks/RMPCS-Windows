using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;

namespace RMPCS.App;

// Shared interactive entry point: installer and Settings use the same pinned verifier.
internal sealed class SensorSetupWindow : Window
{
    private readonly Button install=new(){Content="Download & open official setup",MinWidth=240},close=new(){Content="Close",MinWidth=90,IsCancel=true,IsDefault=true};
    private readonly TextBlock status=new(){Text="Optional setup has not started. You can close this window without making changes.",TextWrapping=TextWrapping.Wrap,Margin=new(0,16,0,12)};
    private readonly ProgressBar progress=new(){Height=7,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new(0,0,0,16)};
    private bool busy;
    internal int ResultCode {get;private set;}=2;
    internal static bool IsAdmin=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    internal const string Review="This optional package adds privileged hardware access for CPU/GPU/motherboard temperature sensors. Basic monitoring does not need it.\n\nRMPCS downloads fixed official LibreHardwareMonitor 0.9.6 libraries and PawnIO 2.2.0 setup. Hashes, protected storage, Windows signature trust/revocation and the pinned namazso.eu publisher must pass before vendor setup opens. Internet access is required.\n\nSigned kernel code may still have defects or firmware/anti-cheat conflicts. A restart may be required. Keep the vendor's administrator-only access defaults. Secure Boot, Memory Integrity and the vulnerable-driver blocklist are never weakened. If Windows blocks the driver, use another provider.\n\nNothing installs silently. Monitoring remains off until you enable in-app reads, discover/select sensors and Apply overlay settings. Cancelling this optional setup leaves RMPCS installed. Shared PawnIO and library files are retained when RMPCS is removed.\n\nLicenses/source: LibreHardwareMonitor MPL-2.0 (github.com/LibreHardwareMonitor/LibreHardwareMonitor); PawnIO GPL-2.0-or-later with device-IO-control exception (github.com/namazso/PawnIO). Full notices accompany RMPCS.";
    internal SensorSetupWindow(Window? owner=null)
    {
        Title="RMPCS · Optional temperature sensor setup";Width=640;Height=670;MinWidth=480;MinHeight=480;WindowStartupLocation=owner?.IsVisible==true?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen;
        if(owner?.IsVisible==true){Owner=owner;ShowInTaskbar=false;Icon=owner.Icon;}
        SetResourceReference(BackgroundProperty,"SurfaceBrush");SetResourceReference(ForegroundProperty,"TextBrush");SetResourceReference(FontFamilyProperty,"UiFontFamily");
        var grid=new Grid{Margin=new(24)};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var title=new TextBlock{Text="Optional temperature sensor package",FontSize=22,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,18)};grid.Children.Add(title);
        var review=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=Review,TextWrapping=TextWrapping.Wrap,FontSize=14}};Grid.SetRow(review,1);grid.Children.Add(review);
        var bottom=new StackPanel();bottom.Children.Add(status);bottom.Children.Add(progress);var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(close);buttons.Children.Add(install);bottom.Children.Add(buttons);Grid.SetRow(bottom,2);grid.Children.Add(bottom);Content=grid;
        close.Click+=(_,_)=>Close();install.Click+=async(_,_)=>await InstallAsync();
        Closing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="Setup is still active. Finish or cancel the vendor installer and let it exit. Downloads have a deadline; no process will be forcibly stopped.";}};
        SourceInitialized+=(_,_)=>ThemeManager.ApplyTitleBar(this);
        ContentRendered+=(_,_)=>close.Focus();
    }
    internal static async Task<int> OpenAsync(Window? owner=null)
    {
        if(IsAdmin){var window=new SensorSetupWindow(owner);window.ShowDialog();return window.ResultCode;}
        try
        {
            var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",WorkingDirectory=AppContext.BaseDirectory};start.ArgumentList.Add("--sensor-setup");
            using var child=Process.Start(start)??throw new InvalidOperationException("Sensor setup did not start.");await child.WaitForExitAsync();return child.ExitCode;
        }
        catch(Win32Exception ex)when(ex.NativeErrorCode==1223){return 2;}
    }
    private async Task InstallAsync()
    {
        if(busy)return;busy=true;install.IsEnabled=close.IsEnabled=false;progress.Visibility=Visibility.Visible;
        try{var outcome=await SensorComponents.InstallAsync(text=>Dispatcher.Invoke(()=>status.Text=text));status.Text=outcome.Message;ResultCode=outcome.RestartRequired?3010:outcome.Ready?0:2;}
        catch(Exception ex)when(ex is not OutOfMemoryException){ResultCode=1;status.Text="Sensor setup stopped: "+ex.Message+" RMPCS remains installed. Use Help > Sensor setup troubleshooting. Windows security settings were not changed.";}
        finally{busy=false;progress.Visibility=Visibility.Collapsed;close.IsEnabled=true;install.IsEnabled=ResultCode is not (0 or 3010);close.Focus();}
    }
    internal void VerifyIdleForChecks(){if(busy||ResultCode!=2||!install.IsEnabled||!close.IsDefault||progress.Visibility!=Visibility.Collapsed||!Review.Contains("Memory Integrity"))throw new InvalidOperationException("Sensor review must default to Close and start idle without downloading or enabling monitoring.");}
}
