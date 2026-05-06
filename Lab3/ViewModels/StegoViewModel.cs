using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab3.Models;
using Lab3.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lab3.ViewModels;

public partial class StegoViewModel : ObservableObject
{
    private readonly ISteganographyService _stegoService;
    private readonly ILogService _log;

    [ObservableProperty] private int _bitsPerChannel = 1;
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private BitmapImage? _previewImage;
    [ObservableProperty] private string _imagePath = string.Empty;
    [ObservableProperty] private string _extractedText = string.Empty;
    [ObservableProperty] private byte[]? _embeddedImageData;

    [ObservableProperty] private EmbeddingMode _embeddingMode = EmbeddingMode.RowMajor;
    [ObservableProperty] private bool _useHillCipher = true;
    [ObservableProperty] private int _hillKeySize = 2;
    [ObservableProperty] private int[,]? _hillKeyMatrix;
    [ObservableProperty] private string _hillKeyDisplay = "Ключ не задан";
    [ObservableProperty] private bool _useZipCompression = true;
    [ObservableProperty] private bool _useHammingCode = false;
    [ObservableProperty] private int _distortionErrorCount = 0;

    public IReadOnlyList<int> BitsOptions { get; } = new List<int> { 1, 2, 3, 4, 5 };
    public IReadOnlyList<int> HillKeySizeOptions { get; } = new List<int> { 2, 3 };

    public StegoViewModel(ISteganographyService stegoService, ILogService log)
    {
        _stegoService = stegoService;
        _log = log;
        _log.LogInfo("[StegoVM] Initialized");
    }

    [RelayCommand]
    private async Task SelectImageAsync()
    {
        _log.LogInfo("[VM] SelectImage command");

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();

        if (file != null)
        {
            ImagePath = file.Path;
            PreviewImage = new BitmapImage(new Uri(file.Path));
            StatusMessage = $"Изображение загружено: {file.Name}";
            _log.LogInfo($"[VM] Image selected: {file.Name}, {file.FileType}");
        }
        else
        {
            _log.LogDebug("[VM] Image selection cancelled");
        }
    }

    [RelayCommand]
    private async Task LoadTextFromFileAsync()
    {
        _log.LogInfo("[VM] LoadTextFromFile command");

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();

        if (file != null)
        {
            InputText = await FileIO.ReadTextAsync(file, Windows.Storage.Streams.UnicodeEncoding.Utf8);
            StatusMessage = $"Текст загружен из {file.Name}";
            _log.LogInfo($"[VM] Text loaded from {file.Name}: {InputText.Length} chars");
        }
    }

    [RelayCommand]
    private async Task EmbedTextAsync()
    {
        _log.LogInfo($"[VM] EmbedText command: text_len={InputText?.Length}, image={Path.GetFileName(ImagePath)}");
        _log.LogInfo($"[VM] Settings: bits={BitsPerChannel}, mode={EmbeddingMode}, Hill={UseHillCipher}, ZIP={UseZipCompression}, Hamming={UseHammingCode}");

        if (string.IsNullOrWhiteSpace(ImagePath) || string.IsNullOrWhiteSpace(InputText))
        {
            StatusMessage = "Выберите изображение и введите текст";
            _log.LogWarning("[VM] Embed failed: missing input");
            return;
        }

        IsBusy = true;
        try
        {
            var settings = new StegoSettings
            {
                BitsPerChannel = BitsPerChannel,
                InputText = InputText,
                ImagePath = ImagePath,
                EmbeddingMode = EmbeddingMode,
                UseHillCipher = UseHillCipher,
                HillKeySize = HillKeySize,
                HillKeyMatrix = HillKeyMatrix,
                UseZipCompression = UseZipCompression,
                UseHammingCode = UseHammingCode,
                DistortionErrorCount = DistortionErrorCount
            };

            _log.LogInfo("[VM] Calling EmbedTextAsync...");
            var result = await _stegoService.EmbedTextAsync(ImagePath, InputText, settings);

            if (result.Success)
            {
                EmbeddedImageData = result.BinaryData;
                StatusMessage = "Текст успешно внедрён. Сохраните изображение.";
                _log.LogInfo($"[VM] Embed success: {result.BinaryData?.Length} bytes");
            }
            else
            {
                StatusMessage = $"Ошибка: {result.ErrorMessage}";
                _log.LogError($"[VM] Embed failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            _log.LogError($"[VM] Embed exception: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _log.LogDebug("[VM] Embed command complete");
        }
    }

    [RelayCommand]
    private async Task ExtractTextAsync()
    {
        _log.LogInfo($"[VM] ExtractText command: image={Path.GetFileName(ImagePath)}");
        _log.LogInfo($"[VM] Settings: bits={BitsPerChannel}, mode={EmbeddingMode}, Hill={UseHillCipher}, ZIP={UseZipCompression}, Hamming={UseHammingCode}");

        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            StatusMessage = "Выберите изображение";
            _log.LogWarning("[VM] Extract failed: no image");
            return;
        }

        IsBusy = true;
        try
        {
            var settings = new StegoSettings
            {
                BitsPerChannel = BitsPerChannel,
                ImagePath = ImagePath,
                EmbeddingMode = EmbeddingMode,
                UseHillCipher = UseHillCipher,
                HillKeySize = HillKeySize,
                HillKeyMatrix = HillKeyMatrix,
                UseZipCompression = UseZipCompression,
                UseHammingCode = UseHammingCode
            };

            _log.LogInfo("[VM] Calling ExtractTextAsync...");
            var result = await _stegoService.ExtractTextAsync(ImagePath, settings);

            if (result.Success)
            {
                ExtractedText = result.Data!;
                StatusMessage = "Текст успешно извлечён!";
                _log.LogInfo($"[VM] Extract success: {ExtractedText.Length} chars");
            }
            else
            {
                StatusMessage = $"Ошибка: {result.ErrorMessage}";
                _log.LogError($"[VM] Extract failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            _log.LogError($"[VM] Extract exception: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _log.LogDebug("[VM] Extract command complete");
        }
    }

    [RelayCommand]
    private async Task SaveImageAsync()
    {
        _log.LogInfo("[VM] SaveImage command");

        if (EmbeddedImageData == null || EmbeddedImageData.Length == 0)
        {
            StatusMessage = "Сначала внедрите текст";
            _log.LogWarning("[VM] Save failed: no image data");
            return;
        }

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeChoices.Add("BMP Image", new List<string> { ".bmp" });
        picker.SuggestedFileName = $"stego_{DateTime.Now:yyyyMMdd_HHmmss}";
        picker.DefaultFileExtension = ".bmp";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteBytesAsync(file, EmbeddedImageData);
            StatusMessage = $"Изображение сохранено: {file.Name}";
            _log.LogInfo($"[VM] Image saved: {file.Name}, {EmbeddedImageData.Length} bytes");
        }
    }

    [RelayCommand]
    private async Task SaveExtractedTextAsync()
    {
        _log.LogInfo("[VM] SaveExtractedText command");

        if (string.IsNullOrWhiteSpace(ExtractedText)) return;

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text File", new List<string> { ".txt" });
        picker.SuggestedFileName = "extracted_text";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, ExtractedText, Windows.Storage.Streams.UnicodeEncoding.Utf8);
            StatusMessage = $"Текст сохранён: {file.Name}";
            _log.LogInfo($"[VM] Text saved: {file.Name}, {ExtractedText.Length} chars");
        }
    }

    // 🔑 Hill key management

    [RelayCommand]
    private void GenerateHillKey()
    {
        _log.LogInfo($"[VM] GenerateHillKey: size={HillKeySize}");

        if (!UseHillCipher) { StatusMessage = "Включите шифрование Хилла"; return; }
        try
        {
            var service = new HillCipherService(_log);
            HillKeyMatrix = service.GenerateRandomKey(HillKeySize, 256);
            HillKeyDisplay = FormatMatrix(HillKeyMatrix);
            StatusMessage = $"Ключ {HillKeySize}x{HillKeySize} сгенерирован";
            _log.LogInfo("[VM] Hill key generated");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка генерации: {ex.Message}";
            _log.LogError($"[VM] Key generation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportHillKeyAsync()
    {
        _log.LogInfo("[VM] ExportHillKey command");

        if (HillKeyMatrix == null) { StatusMessage = "Сначала сгенерируйте или загрузите ключ"; return; }

        var content = $"HILL_CIPHER_KEY\nSIZE={HillKeySize}\nDATA={HillKeyDisplay}";

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Key File", new List<string> { ".key" });
        picker.SuggestedFileName = "hill_key";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, content, Windows.Storage.Streams.UnicodeEncoding.Utf8);
            StatusMessage = $"Ключ экспортирован: {file.Name}";
            _log.LogInfo($"[VM] Key exported: {file.Name}");
        }
    }

    [RelayCommand]
    private async Task ImportHillKeyAsync()
    {
        _log.LogInfo("[VM] ImportHillKey command");

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".key"); picker.FileTypeFilter.Add(".txt");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            var content = await FileIO.ReadTextAsync(file, Windows.Storage.Streams.UnicodeEncoding.Utf8);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            int size = 0;
            string data = string.Empty;

            foreach (var line in lines)
            {
                if (line.StartsWith("SIZE=")) size = int.Parse(line.Substring(5).Trim());
                if (line.StartsWith("DATA=")) data = line.Substring(5).Trim();
            }

            if (size <= 0 || string.IsNullOrWhiteSpace(data))
                throw new FormatException("Неверный формат файла ключа");

            var matrix = ParseMatrix(data, size);
            HillKeySize = size;
            HillKeyMatrix = matrix;
            HillKeyDisplay = FormatMatrix(matrix);
            StatusMessage = $"Ключ импортирован ({size}x{size})";
            _log.LogInfo($"[VM] Key imported: {size}x{size}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка импорта: {ex.Message}";
            _log.LogError($"[VM] Key import failed: {ex.Message}");
        }
    }

    // 🔧 Helpers

    private string FormatMatrix(int[,]? matrix)
    {
        if (matrix == null) return "Ключ не задан";
        int n = matrix.GetLength(0);
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                sb.Append(matrix[i, j].ToString().PadLeft(3));
                if (j < n - 1) sb.Append("   ");
            }
            if (i < n - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    private int[,] ParseMatrix(string data, int size)
    {
        var matrix = new int[size, size];
        var rows = data.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (rows.Length != size) throw new FormatException("Несоответствие размерности");

        for (int i = 0; i < size; i++)
        {
            var cols = rows[i].Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length != size) throw new FormatException("Несоответствие размерности");
            for (int j = 0; j < size; j++)
                matrix[i, j] = int.Parse(cols[j].Trim());
        }
        return matrix;
    }
}