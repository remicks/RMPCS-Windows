using System.Windows;
using System.Windows.Controls;
namespace RMPCS.App;
internal static class Dialogs
{
    public static MessageBoxResult Show(string text,string title) => Show(null,text,title);
    public static MessageBoxResult Show(Window? owner,string text,string title="RMPCS",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None,MessageBoxResult defaultResult=MessageBoxResult.No)
    {
        var app=Application.Current;
        bool early=app.Windows.Count==0;
        var previousMode=app.ShutdownMode;var previousMain=app.MainWindow;
        if(early)app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        try
        {
        var window = new Window { Title=title,SizeToContent=SizeToContent.WidthAndHeight,MinWidth=420,MaxWidth=640,MaxHeight=Math.Max(300,SystemParameters.WorkArea.Height-80),ResizeMode=ResizeMode.NoResize,WindowStartupLocation=owner?.IsVisible==true?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen };
        if(owner?.IsVisible==true){window.Owner=owner;window.ShowInTaskbar=false;window.Icon=owner.Icon;}
        window.SetResourceReference(Window.BackgroundProperty,"SurfaceBrush");window.SetResourceReference(Window.ForegroundProperty,"TextBrush");
        var panel=new StackPanel { Margin=new Thickness(24),MaxWidth=560 };
        panel.Children.Add(new TextBlock { Text=title,FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14),TextWrapping=TextWrapping.Wrap });
        panel.Children.Add(new ScrollViewer {MaxHeight=Math.Max(150,Math.Min(420,SystemParameters.WorkArea.Height-240)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock {Text=text,TextWrapping=TextWrapping.Wrap,FontSize=14}});
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,22,0,0) };
        MessageBoxResult result=buttons==MessageBoxButton.YesNo?MessageBoxResult.No:MessageBoxResult.OK;
        if(buttons==MessageBoxButton.YesNo)
        {
            var cancel=new Button {Content="Cancel",IsDefault=true,IsCancel=true,MinWidth=90};cancel.Click+=(_,_)=>{result=MessageBoxResult.No;window.Close();};
            var proceed=new Button {Content="Continue",MinWidth=100};proceed.Click+=(_,_)=>{result=MessageBoxResult.Yes;window.Close();};
            actions.Children.Add(cancel);actions.Children.Add(proceed);
            window.ContentRendered+=(_,_)=>cancel.Focus();
        }
        else {var ok=new Button {Content="OK",IsDefault=true,IsCancel=true,MinWidth=90};ok.Click+=(_,_)=>window.Close();actions.Children.Add(ok);}
        panel.Children.Add(actions);window.Content=panel;window.SourceInitialized+=(_,_)=>ThemeManager.ApplyTitleBar(window);window.ShowDialog();return result;
        }
        finally {if(early){app.MainWindow=previousMain;app.ShutdownMode=previousMode;}}
    }
    internal static bool VerifyEarlyDialog()
    {
        var original=Application.Current.ShutdownMode;
        var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
        timer.Tick+=(_,_)=>{foreach(Window dialog in Application.Current.Windows){if(dialog.Title!="Startup dialog test")continue;timer.Stop();dialog.Close();break;}};
        timer.Start();
        try{return Show(null,"Startup dialog test only; no action will run.","Startup dialog test",MessageBoxButton.YesNo)==MessageBoxResult.No&&Application.Current.Windows.Count==0&&Application.Current.MainWindow is null&&Application.Current.ShutdownMode==original;}
        finally {timer.Stop();}
    }
}
