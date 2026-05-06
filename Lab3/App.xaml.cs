using Lab3.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace Lab3;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    public static MainWindow? MainWindow => (MainWindow?)Current._window;

    //  Статический доступ к сервисам
    public static IServiceProvider Services => MainWindow?.Services!;

    private Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

        //  Загружаем логи после активации окна (не блокируем старт)
        _ = LoadLogsOnStartupAsync();
    }

    private static async System.Threading.Tasks.Task LoadLogsOnStartupAsync()
    {
        // Небольшая задержка, чтобы окно точно инициализировалось
        await System.Threading.Tasks.Task.Delay(100);
        var logService = Services?.GetService<ILogService>();
        if (logService != null)
            await logService.LoadLogsAsync();
    }
}