using System.Windows;

namespace RMPCS.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        // Load the real XAML and render a frame without running any maintenance.
        if (e.Args.Contains("--smoke-test"))
            window.ContentRendered += (_, _) => { window.Close(); };
        else
            window.ContentRendered += (_, _) => window.CheckAdministrator();
    }
}
