using Lab3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Lab3.Views;

public sealed partial class KnapsackPage : Page
{
    public KnapsackViewModel ViewModel { get; private set; }

    public KnapsackPage()  // 🔥 Параметрless конструктор
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is KnapsackViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}