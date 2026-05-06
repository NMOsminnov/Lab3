using Lab3.Services;
using Lab3.ViewModels;
using Lab3.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Linq;

namespace Lab3;

public sealed partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }
    public IServiceProvider Services { get; }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;

        // 🔍 Диагностика
        System.Diagnostics.Debug.WriteLine($"=== Debug Info ===");
        System.Diagnostics.Debug.WriteLine($"NavView is null: {NavView == null}");
        System.Diagnostics.Debug.WriteLine($"ContentFrame is null: {ContentFrame == null}");
        System.Diagnostics.Debug.WriteLine($"NavView.Content type: {NavView?.Content?.GetType().Name}");

        Services = ConfigureServices();

        try
        {
            var vm = Services.GetRequiredService<StegoViewModel>();
            System.Diagnostics.Debug.WriteLine($"ViewModel created: {vm != null}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ ViewModel error: {ex.Message}");
        }

        NavView.ItemInvoked += OnNavItemInvoked;

        // 🔥 Безопасная навигация
        if (ContentFrame != null)
        {
            NavView.SelectedItem = NavView.MenuItems?.FirstOrDefault();
            var viewModel = Services.GetRequiredService<StegoViewModel>();
            ContentFrame.Navigate(typeof(StegoPage), viewModel);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("❌ ContentFrame is NULL! См. решение ниже.");
        }
    }

    private IServiceProvider ConfigureServices()
    {
        var collection = new ServiceCollection();

        // Сервисы
        collection.AddSingleton<ISteganographyService, SteganographyService>();
        collection.AddSingleton<IKnapsackService, KnapsackService>();
        collection.AddSingleton<IRsaService, RsaService>();
        collection.AddSingleton<ILogService, LogService>();
        collection.AddSingleton<SteganalysisService>();


        // ViewModels
        collection.AddTransient<StegoViewModel>();
        collection.AddTransient<KnapsackViewModel>();
        collection.AddTransient<RsaViewModel>();
        collection.AddTransient<LogsViewModel>();
        collection.AddTransient<SteganalysisViewModel>();

        return collection.BuildServiceProvider();
    }

    private void OnNavItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is not string tag) return;

        // 🔥 ФИКС 1: явно указываем тип object? вместо var
        object? viewModel = tag switch
        {
            "Stego" => Services.GetRequiredService<StegoViewModel>(),
            "Knapsack" => Services.GetRequiredService<KnapsackViewModel>(),
            "RSA" => Services.GetRequiredService<RsaViewModel>(),
            "Logs" => Services.GetRequiredService<LogsViewModel>(),
            "StegoAnalysis" => Services.GetRequiredService<SteganalysisViewModel>(),
            _ => null
        };

        // 🔥 ФИКС 2: явно указываем тип Type для page
        Type pageType = tag switch
        {
            "Stego" => typeof(StegoPage),
            "Knapsack" => typeof(KnapsackPage),
            "RSA" => typeof(RsaPage),
            "Logs" => typeof(LogsPage),
            "StegoAnalysis" => typeof(SteganalysisPage),
            _ => typeof(StegoPage)
        };

        // 🔥 ФИКС 3: viewModel — это 2-й параметр, 3-й — анимация
        ContentFrame.Navigate(pageType, viewModel, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
    }
}