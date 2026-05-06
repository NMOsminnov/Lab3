using Lab3.Services;
using Lab3.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;  // 🔥 Важно!

namespace Lab3.Views;

public sealed partial class StegoPage : Page
{
    public StegoViewModel ViewModel { get; private set; }

    // 🔥 1. Параметрless конструктор для XAML-активации
    public StegoPage()
    {
        InitializeComponent();
    }

    // 🔥 2. Получаем ViewModel из навигационного параметра
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is StegoViewModel vm)
        {
            ViewModel = vm;
            DataContext = vm;


            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(StegoViewModel.StatusMessage) &&
                    !string.IsNullOrWhiteSpace(vm.StatusMessage))
                {
                    App.Services.GetService<ILogService>()?.LogInfo(vm.StatusMessage);
                }
            };
        }
    }

    private void RadioButton_RowMajor_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel != null)
            ViewModel.EmbeddingMode = EmbeddingMode.RowMajor;
    }

    private void RadioButton_ColumnMajor_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel != null)
            ViewModel.EmbeddingMode = EmbeddingMode.ColumnMajor;
    }
}