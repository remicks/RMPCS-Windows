using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RMPCS.Core;
namespace RMPCS.App;
public partial class MainWindow
{
    internal void PreviewAppearance(string mode,string accent,bool compact=false,bool sidebar=false)
    {
        if(!TestMode)return;
        preferences=preferences with {ThemeMode=ThemeCatalog.Modes.Contains(mode)?mode:"Light",AccentName=ThemeCatalog.Presets.Any(x=>x.Name==accent)?accent:"Crimson Red"};
        PutAppearanceControls();ApplyPreferences();
        if(compact){Width=1000;Height=720;}
        if(sidebar){preferences=preferences with {SidebarEnabled=true};PutSidebarControls();ApplySidebarPreferences();}
        if(MainTabs.SelectedIndex==3)
        {
            StartupItem Item(string name,bool enabled)=>new(StartupPolicy.Id("User64",name,"S-1-5-21-1"),name,"Preview only - no command is run","User64",enabled,false,"S-1-5-21-1");
            StartupEntries.ItemsSource=new[]{Item("Example browser",true),Item("Example updater",false)};
            StartupListStatus.Text="Preview examples only. No real startup entry has been read or changed.";
        }
    }
    internal void VerifyThemeUi(string output,bool earlyDialogVerified)
    {
        int checks=0;
        void Check(bool condition,string label){if(!condition)throw new InvalidDataException(label);checks++;}
        Check(earlyDialogVerified,"closing a pre-window dialog preserves application startup");
        Check(typeof(MainWindow).Assembly.GetCustomAttributes(typeof(System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute),false).OfType<System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute>().SingleOrDefault()?.Paths==System.Runtime.InteropServices.DllImportSearchPath.System32,"native imports are restricted to Windows System32");
        string ColorOf(object value)=>value is SolidColorBrush brush?brush.Color.ToString():"unsupported";
        foreach(string mode in new[]{"Light","Dark"})
        foreach(var preset in ThemeCatalog.Presets)
        {
            ThemeManager.Apply(new(){ThemeMode=mode,AccentName=preset.Name});
            UpdateLayout();
            if(!SystemParameters.HighContrast)
            {
                Check(ColorOf(Background)==(mode=="Dark"?"#FF111019":"#FFF5F7FA"),"live window brush");
                Check(ColorOf(Activity.Background)==(mode=="Dark"?"#FF171522":"#FFFFFFFF"),"live text surface");
                Check(ColorOf(Activity.Foreground)==(mode=="Dark"?"#FFF4F0FF":"#FF202938"),"live text foreground");
                Check(ColorOf(Application.Current.Resources["AccentBrush"])=="#FF"+preset.Hex[1..],"preset accent");
            }
        }
        MainTabs.SelectedIndex=5;UpdateLayout();CloseToTrayChoice.ApplyTemplate();
        Check(CloseToTrayChoice.Template.FindName("Track",CloseToTrayChoice) is Border,"settings use switch template");
        Care_Click(this,new RoutedEventArgs());Check(MainTabs.SelectedIndex==1&&!busy,"recommended care reviews without running");
        Check(RunButton.Visibility==Visibility.Visible,"run button visible on maintenance");
        MainTabs.SelectedIndex=3;UpdateLayout();Check(RunButton.Visibility==Visibility.Collapsed,"run action hidden outside maintenance");
        var fixture=new StartupItem(StartupPolicy.Id("User64","Example","S-1-5-21-1"),"Example","preview only","User64",true,false,"S-1-5-21-1");
        var toggle=new CheckBox {DataContext=fixture,IsChecked=false};StartupToggle_Click(toggle,new RoutedEventArgs());
        Check(toggle.IsChecked==true&&StartupListStatus.Text.Contains("Administrator"),"test/denied startup switch returns to original without mutation");
        bool safeDefault=false;
        var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
        timer.Tick+=(_,_)=>
        {
            foreach(Window dialog in Application.Current.Windows)
            {
                if(dialog.Title!="Confirmation test")continue;
                timer.Stop();
                if(dialog.Content is StackPanel panel&&panel.Children[panel.Children.Count-1] is StackPanel buttons)
                    safeDefault=buttons.Children.OfType<Button>().Count(b=>b.IsDefault)==1&&buttons.Children.OfType<Button>().Any(b=>b.IsDefault&&b.IsCancel&&b.Content as string=="Cancel");
                dialog.Close();break;
            }
        };
        timer.Start();
        try {Check(Dialogs.Show(this,"Theme and safe-default test only. No action will run.","Confirmation test",MessageBoxButton.YesNo)==MessageBoxResult.No,"closing a themed confirmation does not approve an action");}
        finally {timer.Stop();}
        Check(safeDefault,"confirmation default and Escape action are cancellation");
        ThemeManager.Apply(preferences);
        File.WriteAllText(output,$"{checks} WPF theme/switch checks passed. No preferences, startup entries, schedules or maintenance were changed.\n");
    }
}
