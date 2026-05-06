using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab3.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lab3.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILogService _logService;

    public ObservableCollection<string> Logs => _logService.Logs;

    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _logCountText = string.Empty;

    public event EventHandler? OnNewLogAdded;

    //  В классе LogsViewModel добавь:
    public string LogsText => string.Join("\n", Logs);

    //  И обновляй уведомление при изменении коллекции:
    public LogsViewModel(ILogService logService)
    {
        _logService = logService;
        UpdateCountText();

        _logService.Logs.CollectionChanged += (s, e) =>
        {
            UpdateCountText();
            OnPropertyChanged(nameof(LogsText)); // 🔥 Обновляем текст в UI

            if (AutoScroll && e.NewItems?.Count > 0)
                OnNewLogAdded?.Invoke(this, EventArgs.Empty);
        };
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(string.Join("\n", Logs));
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        StatusText = $"Скопировано {Logs.Count} записей";
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _logService.LoadLogsAsync();
        UpdateCountText();
        StatusText = "Логи обновлены";
    }


    [RelayCommand]
    private void Clear()
    {
        _logService.ClearLogs();
        UpdateCountText();
        StatusText = "Логи очищены";
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text File", new[] { ".txt" });
        picker.SuggestedFileName = $"logs_{DateTime.Now:yyyyMMdd_HHmmss}";

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteLinesAsync(file, Logs);
            StatusText = $"Экспортировано {Logs.Count} записей";
        }
    }

    private void UpdateCountText() => LogCountText = $"Записей: {Logs.Count}";
}