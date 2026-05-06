using Lab3.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Lab3.Views;

public sealed partial class LogsPage : Page
{
    public LogsViewModel ViewModel { get; private set; }

    public LogsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is LogsViewModel vm)
        {
            ViewModel = vm;
            DataContext = vm;
            ViewModel.OnNewLogAdded += ViewModel_OnNewLogAdded;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (ViewModel != null)
            ViewModel.OnNewLogAdded -= ViewModel_OnNewLogAdded;
        base.OnNavigatedFrom(e);
    }

    private void ViewModel_OnNewLogAdded(object? sender, EventArgs e)
    {
        if (ViewModel?.AutoScroll == true && LogBox != null)
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                //  Прокрутка TextBox вниз
                LogBox.SelectionStart = LogBox.Text.Length;
            });
        }
    }
}