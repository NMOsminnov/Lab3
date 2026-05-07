using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab3.Models;
using Lab3.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lab3.Views;

public partial class RsaViewModel : ObservableObject
{
    private readonly IRsaService _rsaService;
    private readonly FrequencyAnalyzer _frequencyAnalyzer = new();
    private byte[]? _encryptedBinaryData;

    [ObservableProperty] private int _minPrime = 50;
    [ObservableProperty] private int _maxPrime = 200;
    [ObservableProperty] private BigInteger _multiplier; // E
    [ObservableProperty] private BigInteger _modulus;    // N
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private string _keysInfo = string.Empty;
    [ObservableProperty] private string _encryptedText = string.Empty;
    [ObservableProperty] private string _decryptedText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private Visibility _frequencyPanelVisibility = Visibility.Collapsed;

    [ObservableProperty] private ObservableCollection<FrequencyItem> _frequencyTable;
    [ObservableProperty] private FrequencyStats _frequencyStats;

    [ObservableProperty] private RsaKeys? _currentKeys;

    public RsaViewModel(IRsaService rsaService)
    {
        _rsaService = rsaService;
        FrequencyTable = new ObservableCollection<FrequencyItem>();
        _frequencyStats = new FrequencyStats();
    }

    [RelayCommand]
    private async Task GenerateKeysAsync()
    {
        if (MinPrime < 2 || MaxPrime > 1000 || MinPrime >= MaxPrime)
        {
            StatusMessage = "Неверный диапазон простых чисел";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _rsaService.GenerateKeysAsync(MinPrime, MaxPrime);
            if (result.Success)
            {
                // 🔥 Извлекаем RsaKeys из Payload
                _currentKeys = result.Payload as RsaKeys;
                if (_currentKeys == null)
                    throw new Exception("Не удалось получить ключи");

                Multiplier = _currentKeys.E;
                Modulus = _currentKeys.N;
                KeysInfo = result.Data!;  // Текст с информацией
                StatusMessage = "Ключи RSA сгенерированы";
            }
            else
            {
                StatusMessage = $"Ошибка: {result.ErrorMessage}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EncryptAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || _currentKeys == null)
        {
            StatusMessage = "Введите текст и сгенерируйте ключи";
            return;
        }

        IsBusy = true;
        try
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(InputText);

            // 🔹 Подпись всегда включена: используем тот же ключ для подписи (приватный)
            var result = await _rsaService.EncryptAsync(
                dataBytes,
                _currentKeys,  // публичный для шифрования
                _currentKeys.D != 0 ? _currentKeys : null  // приватный для подписи
            );

            if (result.Success && result.BinaryData != null)
            {
                _encryptedBinaryData = result.BinaryData;
                var blocks = DeserializeBigIntegers(result.BinaryData);
                EncryptedText = string.Join(" ", blocks.Select(x => x.ToString()));
                StatusMessage = "✅ Шифрование + подпись выполнены";
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
    private async Task DecryptAsync()
    {
        if (string.IsNullOrWhiteSpace(EncryptedText) || _currentKeys == null)
        {
            StatusMessage = "Нет зашифрованных данных или ключей";
            return;
        }

        IsBusy = true;
        try
        {
            var blocks = EncryptedText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => BigInteger.Parse(x.Trim()))
                .ToList();
            byte[] cipherBytes = SerializeBigIntegers(blocks);

            // 🔹 Проверка подписи всегда включена: используем тот же ключ для проверки (публичный)
            var result = await _rsaService.DecryptAsync(
                cipherBytes,
                _currentKeys,  // приватный для расшифровки
                _currentKeys.E != 0 ? _currentKeys : null  // публичный для проверки
            );

            if (result.Success && result.BinaryData != null)
            {
                DecryptedText = Encoding.UTF8.GetString(result.BinaryData);
                StatusMessage = result.Data!;  // "Расшифровано и проверено" или ошибка подписи
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
    private async Task LoadTextFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        await InitPickerAsync(picker);
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            InputText = await FileIO.ReadTextAsync(file);
            StatusMessage = $"Текст загружен: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task LoadEncryptedFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        await InitPickerAsync(picker);
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            EncryptedText = await FileIO.ReadTextAsync(file);
            StatusMessage = $"Шифротекст загружен: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task SaveEncryptedTextAsync()
    {
        if (string.IsNullOrWhiteSpace(EncryptedText))
        {
            StatusMessage = "Нет данных для сохранения";
            return;
        }

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Text File", new List<string> { ".txt" });
        picker.SuggestedFileName = "rsa_encrypted";
        await InitPickerAsync(picker);
        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, EncryptedText);
            StatusMessage = $"Файл сохранён: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task SaveDecryptedTextAsync()
    {
        if (string.IsNullOrWhiteSpace(DecryptedText))
        {
            StatusMessage = "Нет данных для сохранения";
            return;
        }

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Text File", new List<string> { ".txt" });
        picker.SuggestedFileName = "rsa_decrypted";
        await InitPickerAsync(picker);
        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, DecryptedText);
            StatusMessage = $"Файл сохранён: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task ExportPublicKeyAsync()
    {
        if (_currentKeys == null)
        {
            StatusMessage = "Сначала сгенерируйте ключи";
            return;
        }

        var content = $"ALGORITHM=RSA\nTYPE=PUBLIC\nE={_currentKeys.E}\nN={_currentKeys.N}";

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Key File", new List<string> { ".txt" });
        picker.SuggestedFileName = "rsa_public_key";
        await InitPickerAsync(picker);
        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, content);
            StatusMessage = $"Публичный ключ сохранён: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task ImportPublicKeyAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        await InitPickerAsync(picker);
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var content = await FileIO.ReadTextAsync(file);
                var lines = content.Split('\n');

                if (!lines.Any(l => l.StartsWith("ALGORITHM=RSA")))
                    throw new Exception("Неверный формат файла");

                var e = BigInteger.Parse(lines.First(l => l.StartsWith("E=")).Split('=')[1]);
                var n = BigInteger.Parse(lines.First(l => l.StartsWith("N=")).Split('=')[1]);

                _currentKeys = new RsaKeys { E = e, N = n, D = 0, PrimeP = 0, PrimeQ = 0 };
                Multiplier = e;
                Modulus = n;
                KeysInfo = $"Публичный ключ загружён\ne={e}\nn={n}";
                StatusMessage = "Публичный ключ импортирован";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка импорта: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task ExportPrivateKeyAsync()
    {
        if (_currentKeys == null)
        {
            StatusMessage = "Сначала сгенерируйте ключи";
            return;
        }

        var content = $"ALGORITHM=RSA\nTYPE=PRIVATE\nE={_currentKeys.E}\nN={_currentKeys.N}\nD={_currentKeys.D}\nP={_currentKeys.PrimeP}\nQ={_currentKeys.PrimeQ}";

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Key File", new List<string> { ".txt" });
        picker.SuggestedFileName = "rsa_private_key";
        await InitPickerAsync(picker);
        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, content);
            StatusMessage = $"Приватный ключ сохранён: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task ImportPrivateKeyAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        await InitPickerAsync(picker);
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var content = await FileIO.ReadTextAsync(file);
                var lines = content.Split('\n');

                if (!lines.Any(l => l.StartsWith("ALGORITHM=RSA")) ||
                    !lines.Any(l => l.StartsWith("TYPE=PRIVATE")))
                    throw new Exception("Неверный формат файла");

                _currentKeys = new RsaKeys
                {
                    E = BigInteger.Parse(lines.First(l => l.StartsWith("E=")).Split('=')[1]),
                    N = BigInteger.Parse(lines.First(l => l.StartsWith("N=")).Split('=')[1]),
                    D = BigInteger.Parse(lines.First(l => l.StartsWith("D=")).Split('=')[1]),
                    PrimeP = int.Parse(lines.First(l => l.StartsWith("P=")).Split('=')[1]),
                    PrimeQ = int.Parse(lines.First(l => l.StartsWith("Q=")).Split('=')[1])
                };

                Multiplier = _currentKeys.E;
                Modulus = _currentKeys.N;
                KeysInfo = $"Приватный ключ загружён\np={_currentKeys.PrimeP}, q={_currentKeys.PrimeQ}\nn={_currentKeys.N}";
                StatusMessage = "Приватный ключ импортирован";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка импорта: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void AnalyzeFrequency()
    {
        if (string.IsNullOrWhiteSpace(EncryptedText))
        {
            StatusMessage = "Сначала зашифруйте текст";
            return;
        }

        var items = _frequencyAnalyzer.Analyze(EncryptedText);
        FrequencyTable = new ObservableCollection<FrequencyItem>(items.Take(100));
        FrequencyStats = _frequencyAnalyzer.GetStats(EncryptedText);

        FrequencyPanelVisibility = Visibility.Visible;
        StatusMessage = $"Анализ выполнен: {FrequencyStats.UniqueBlocks} уникальных блоков";
    }

    [RelayCommand]
    private void CloseFrequencyPanel()
    {
        FrequencyPanelVisibility = Visibility.Collapsed;
    }

    // Вспомогательные методы
    private byte[] SerializeBigIntegers(List<BigInteger> values)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(values.Count);
        foreach (var val in values)
        {
            var bytes = val.ToByteArray(isUnsigned: true, isBigEndian: true);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        return ms.ToArray();
    }

    private List<BigInteger> DeserializeBigIntegers(byte[] data)
    {
        var result = new List<BigInteger>();
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int len = reader.ReadInt32();
            var bytes = reader.ReadBytes(len);
            result.Add(new BigInteger(bytes, isUnsigned: true, isBigEndian: true));
        }
        return result;
    }

    private async Task InitPickerAsync(FileOpenPicker picker)
    {
        var window = App.MainWindow;
        if (window != null)
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
    }

    private async Task InitPickerAsync(FileSavePicker picker)
    {
        var window = App.MainWindow;
        if (window != null)
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
    }
}