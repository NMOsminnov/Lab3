using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab3.Models;
using Lab3.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lab3.ViewModels;

public partial class SteganalysisViewModel : ObservableObject
{
    private readonly SteganalysisService _analysisService;
    private readonly ILogService _log;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private double _threshold = 25.0;
    [ObservableProperty] private string _imagePath1 = string.Empty;
    [ObservableProperty] private string _imagePath2 = string.Empty;

    [ObservableProperty] private ObservableCollection<SteganalysisPoint> _chartData1Bit = new();
    [ObservableProperty] private ObservableCollection<SteganalysisPoint> _chartData2Bit = new();
    [ObservableProperty] private ObservableCollection<SteganalysisPoint> _chartData3Bit = new();

    [ObservableProperty] private ObservableCollection<SteganalysisResult> _lastResults = new();

    public SteganalysisViewModel(SteganalysisService analysisService, ILogService log)
    {
        _analysisService = analysisService;
        _log = log;
        _log.LogInfo("[StegoAnalysisVM] Initialized");
    }

    [RelayCommand]
    private async Task SelectImage1Async()
    {
        var path = await PickImageAsync("Выберите первое изображение");
        if (!string.IsNullOrEmpty(path)) ImagePath1 = path;
    }

    [RelayCommand]
    private async Task SelectImage2Async()
    {
        var path = await PickImageAsync("Выберите второе изображение");
        if (!string.IsNullOrEmpty(path)) ImagePath2 = path;
    }

    [RelayCommand]
    private async Task RunAnalysisAsync()
    {
        if (string.IsNullOrEmpty(ImagePath1) || string.IsNullOrEmpty(ImagePath2))
        {
            StatusMessage = "Выберите оба изображения";
            return;
        }

        IsBusy = true;
        StatusMessage = "Запуск анализа...";
        _log.LogInfo("[VM] Starting steganalysis study");

        try
        {
            var imagePaths = new List<string> { ImagePath1, ImagePath2 };
            var bitOptions = new List<int> { 1, 2, 3 };
            var fillLevels = new List<double> { 20, 40, 60, 80, 100 };

            var reports = await _analysisService.RunFullStudyAsync(
                imagePaths, bitOptions, fillLevels, Threshold);

            // 🔹 Заполняем данные для графиков
            ChartData1Bit.Clear();
            ChartData2Bit.Clear();
            ChartData3Bit.Clear();

            foreach (var report in reports)
            {
                var target = report.BitsPerChannel switch
                {
                    1 => ChartData1Bit,
                    2 => ChartData2Bit,
                    3 => ChartData3Bit,
                    _ => null
                };
                if (target != null)
                    foreach (var point in report.Points)
                        target.Add(point);
            }

            StatusMessage = "Анализ завершён. Графики обновлены.";
            _log.LogInfo("[VM] Analysis complete");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            _log.LogError($"[VM] Analysis failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AnalyzeSingleAsync()
    {
        if (string.IsNullOrEmpty(ImagePath1)) return;

        IsBusy = true;
        try
        {
            LastResults.Clear();
            foreach (int bits in new[] { 1, 2, 3 })
            {
                var result = await _analysisService.AnalyzeImageAsync(ImagePath1, bits, Threshold);
                LastResults.Add(result);
            }
            StatusMessage = "Анализ одного изображения завершён";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task<string> PickImageAsync(string title)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".png");

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}