using Lab3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;


namespace Lab3.Views;

public sealed partial class RsaPage : Page
{
    public RsaViewModel ViewModel { get; private set; }  // ✅ Должно быть public

    public RsaPage()
    {
        InitializeComponent();  // ✅ Теперь сработает после исправления XAML
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is RsaViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}