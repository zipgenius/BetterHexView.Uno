using Microsoft.UI.Xaml;

namespace BetterHexViewer.Uno.Demo;

public partial class App : Application
{
    private Window? _window;
    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "BetterHexViewer.Uno", Content = new MainPage() };
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1280, Height = 820 });
        _window.Activate();
    }
}
