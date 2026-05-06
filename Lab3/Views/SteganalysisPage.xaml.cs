using Lab3.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Lab3.Views;

public sealed partial class SteganalysisPage : Page
{
    public SteganalysisViewModel ViewModel { get; private set; }

    public SteganalysisPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SteganalysisViewModel vm)
        {
            ViewModel = vm;
            DataContext = vm;
        }
    }
}