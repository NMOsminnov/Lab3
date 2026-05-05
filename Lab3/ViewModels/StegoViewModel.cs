using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab3.Models;
using Lab3.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lab3.ViewModels;

public partial class StegoViewModel : ObservableObject
{
    private readonly ISteganographyService _stegoService;

    [ObservableProperty] private int _bitsPerChannel = 1;
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private BitmapImage? _previewImage;
    [ObservableProperty] private string _imagePath = string.Empty;
    [ObservableProperty] private string _extractedText = string.Empty;
    // 🔥 Добавьте поле для хранения изображения
    [ObservableProperty] private byte[]? _embeddedImageData;

    public IReadOnlyList<int> BitsOptions { get; } = new List<int> { 1, 2, 3, 4 };


    public StegoViewModel(ISteganographyService stegoService)
    {
        _stegoService = stegoService;
    }

    [RelayCommand]
    private async Task SelectImageAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            ImagePath = file.Path;
            var bitmap = new BitmapImage(new Uri(file.Path));
            PreviewImage = bitmap;
            StatusMessage = $"Изображение загружено: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task LoadTextFromFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            // 🔥 Явно указываем UTF-8 кодировку!
            InputText = await Windows.Storage.FileIO.ReadTextAsync(
                file,
                Windows.Storage.Streams.UnicodeEncoding.Utf8);
            StatusMessage = $"Текст загружен из {file.Name}";
        }
    }


    [RelayCommand]
    private async Task EmbedTextAsync()
    {
        if (string.IsNullOrWhiteSpace(ImagePath) || string.IsNullOrWhiteSpace(InputText))
        {
            StatusMessage = "Выберите изображение и введите текст";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _stegoService.EmbedTextAsync(ImagePath, InputText, BitsPerChannel);
            if (result.Success)
            {
                EmbeddedImageData = result.BinaryData;  // 🔥 Сохраняем байты!
                StatusMessage = "✅ Текст внедрён! Нажмите 💾 для сохранения.";
            }
            else
            {
                StatusMessage = $"❌ Ошибка: {result.ErrorMessage}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExtractTextAsync()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            StatusMessage = "Выберите изображение";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _stegoService.ExtractTextAsync(ImagePath, BitsPerChannel);
            if (result.Success)
            {
                ExtractedText = result.Data!;
                StatusMessage = "✅ Текст успешно извлечён!";
            }
            else
            {
                StatusMessage = $"❌ Ошибка: {result.ErrorMessage}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveImageAsync()
    {
        // 🔥 Проверяем, есть ли данные для сохранения
        if (EmbeddedImageData == null || EmbeddedImageData.Length == 0)
        {
            StatusMessage = "❌ Сначала внедрите текст в изображение!";
            return;
        }

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeChoices.Add("PNG", new[] { ".png" });
        picker.SuggestedFileName = $"stego_{DateTime.Now:yyyyMMdd_HHmmss}";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            try
            {
                // 🔥 Записываем байты напрямую в файл
                await FileIO.WriteBytesAsync(file, EmbeddedImageData);
                StatusMessage = $"✅ Изображение сохранено: {file.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Ошибка: {ex.Message}";
            }
        }
    }
    [RelayCommand]
    private async Task SaveExtractedTextAsync()
    {
        if (string.IsNullOrWhiteSpace(ExtractedText)) return;

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text", new[] { ".txt" });
        picker.SuggestedFileName = "extracted_text";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            // 🔥 Также указываем UTF-8 при записи
            await Windows.Storage.FileIO.WriteTextAsync(
                file,
                ExtractedText,
                Windows.Storage.Streams.UnicodeEncoding.Utf8);
            StatusMessage = $"Текст сохранён: {file.Name}";
        }
    }
}