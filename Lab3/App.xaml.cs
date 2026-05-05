using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lab3;

public partial class App : Application
{
    // 🔥 Статическое свойство для доступа к окну
    public static new App Current => (App)Application.Current;
    public static MainWindow? MainWindow => (MainWindow?)Current._window;

    private Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}