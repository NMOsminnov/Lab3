using Lab3.ViewModels;
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

        if (e.Parameter is StegoViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;  // Для {x:Bind}
        }
    }
}