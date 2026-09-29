using Microsoft.UI.Xaml;

namespace GeopoliticalMonitor.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.Activate();
    }
}
