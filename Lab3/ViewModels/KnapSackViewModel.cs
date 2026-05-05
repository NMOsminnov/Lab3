// Lab3/ViewModels/KnapsackViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab3.Models;
using Lab3.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;  // 🔥 Добавляем
using System.Text;      // 🔥 Добавляем
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.UI.Xaml;    

namespace Lab3.ViewModels;

public partial class KnapsackViewModel : ObservableObject
{
    private readonly IKnapsackService _knapsackService;

    [ObservableProperty] private int _sequenceLength = 200; // 🔥 По умолчанию 200

    // 🔥 Меняем long на BigInteger для параметров ключей
    [ObservableProperty] private BigInteger _multiplier;
    [ObservableProperty] private BigInteger _modulus;

    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private string _superincreasingSequence = string.Empty;
    [ObservableProperty] private string _publicKey = string.Empty;
    [ObservableProperty] private string _encryptedText = string.Empty; // Для отображения (строка)
    [ObservableProperty] private string _decryptedText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    // 🔥 Меняем List<long> на List<BigInteger> для реальных ключей
    [ObservableProperty] private List<BigInteger>? _privateKeyList;
    [ObservableProperty] private List<BigInteger>? _publicKeyList;



    [ObservableProperty] private ObservableCollection<FrequencyItem> _frequencyTable;
    [ObservableProperty] private FrequencyStats _frequencyStats;
    [ObservableProperty] private Visibility _frequencyPanelVisibility = Visibility.Collapsed;


    private readonly FrequencyAnalyzer _frequencyAnalyzer = new();


    // 🔥 Добавляем поле для хранения зашифрованных байтов (для сохранения в файл)
    private byte[]? _encryptedBinaryData;

    public KnapsackViewModel(IKnapsackService knapsackService)
    {
        _knapsackService = knapsackService;
    }

    [RelayCommand]
    private async Task GenerateKeysAsync()
    {
        // 🔥 Обновляем проверку на 200
        if (SequenceLength is < 1 or > 200)
        {
            StatusMessage = "Длина последовательности: 1-200";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _knapsackService.GenerateKeysAsync(SequenceLength);
            if (result.Success)
            {
                // 🔥 Сохраняем ключи с правильными типами
                PrivateKeyList = result.PrivateKey;
                PublicKeyList = result.PublicKey;

                // 🔥 Сохраняем параметры с правильными типами
                Multiplier = result.Multiplier;
                Modulus = result.Modulus;

                // Строки для отображения в UI
                if (!string.IsNullOrEmpty(result.Data))
                {
                    var lines = result.Data.Split('\n');
                    SuperincreasingSequence = lines.Length > 0 ? lines[0].Replace("Private: ", "") : "";
                    PublicKey = lines.Length > 1 ? lines[1].Replace("Public: ", "") : "";
                }

                StatusMessage = "✅ Ключи сгенерированы";
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
    private async Task EncryptAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || PublicKeyList == null)
        {
            StatusMessage = "Введите текст и сгенерируйте ключи";
            return;
        }

        IsBusy = true;
        try
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(InputText);

            var settings = new KnapsackSettings
            {
                PublicKey = PublicKeyList,
                Multiplier = Multiplier,
                Modulus = Modulus
            };

            var result = await _knapsackService.EncryptAsync(dataBytes, settings);

            if (result.Success && result.BinaryData != null)
            {
                // Конвертируем бинарные данные в текстовый формат: числа через пробел
                var blocks = DeserializeBigIntegers(result.BinaryData);
                EncryptedText = string.Join(" ", blocks.Select(x => x.ToString()));
                StatusMessage = "Шифрование выполнено";
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
    private async Task DecryptAsync()
    {
        if (string.IsNullOrWhiteSpace(EncryptedText) || PrivateKeyList == null)
        {
            StatusMessage = "Нет зашифрованных данных или закрытого ключа";
            return;
        }

        IsBusy = true;
        try
        {
            // Парсим текстовый шифротекст обратно в список BigInteger
            var blocks = EncryptedText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => BigInteger.Parse(x.Trim()))
                .ToList();

            // Сериализуем в байты для передачи в сервис
            byte[] cipherBytes = SerializeBigIntegers(blocks);

            var settings = new KnapsackSettings
            {
                SuperincreasingSequence = PrivateKeyList,
                Multiplier = Multiplier,
                Modulus = Modulus
            };

            var result = await _knapsackService.DecryptAsync(cipherBytes, settings);

            if (result.Success && result.BinaryData != null)
            {
                DecryptedText = Encoding.UTF8.GetString(result.BinaryData);
                StatusMessage = "Расшифровка выполнена";
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
    private async Task LoadFromFileAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");

            var window = App.MainWindow;
            if (window == null)
            {
                StatusMessage = "❌ Окно не найдено";
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync().AsTask();

            if (file != null)
            {
                // 🔥 Читаем как текст (для текстовых файлов)
                InputText = await FileIO.ReadTextAsync(file).AsTask();
                StatusMessage = $"Текст загружен из {file.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка: {ex.Message}";
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
        picker.SuggestedFileName = "encrypted";

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, EncryptedText);
            StatusMessage = $"Файл сохранён: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task LoadEncryptedTextAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            EncryptedText = await FileIO.ReadTextAsync(file);
            StatusMessage = $"Файл загружен: {file.Name}";
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
        picker.SuggestedFileName = "decrypted";

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

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
        if (PublicKeyList == null)
        {
            StatusMessage = "Сначала сгенерируйте ключи";
            return;
        }

        var lines = new List<string>
    {
        "ALGORITHM=Knapsack",
        $"LENGTH={SequenceLength}",
        $"MULTIPLIER={Multiplier}",
        $"MODULUS={Modulus}",
        $"PUBKEY={string.Join(",", PublicKeyList.Select(x => x.ToString()))}"
    };
        var content = string.Join("\n", lines);

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Key File", new List<string> { ".txt" });
        picker.SuggestedFileName = "public_key";

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, content);
            StatusMessage = $"Ключ экспортирован: {file.Name}";
        }
    }

    [RelayCommand]
    private async Task ImportPublicKeyAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var content = await FileIO.ReadTextAsync(file);
                var lines = content.Split('\n').Select(l => l.Trim()).ToList();

                if (!lines.Any(l => l.StartsWith("ALGORITHM=Knapsack")))
                    throw new Exception("Неверный формат файла");

                Multiplier = BigInteger.Parse(lines.First(l => l.StartsWith("MULTIPLIER=")).Split('=')[1]);
                Modulus = BigInteger.Parse(lines.First(l => l.StartsWith("MODULUS=")).Split('=')[1]);
                SequenceLength = int.Parse(lines.First(l => l.StartsWith("LENGTH=")).Split('=')[1]);
                PublicKeyList = lines.First(l => l.StartsWith("PUBKEY="))
                    .Split('=')[1]
                    .Split(',')
                    .Select(BigInteger.Parse)
                    .ToList();

                PublicKey = $"Загружен: {SequenceLength} элементов";
                StatusMessage = "Открытый ключ импортирован";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка импорта: {ex.Message}";
            }
        }
    }

    private List<BigInteger> DeserializeBigIntegers(byte[] data)
    {
        var result = new List<BigInteger>();
        if (data == null || data.Length < 4) return result;

        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        try
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count && ms.Position < ms.Length; i++)
            {
                int len = reader.ReadInt32();
                if (len <= 0 || len > 1024) break; // защита от мусора
                var bytes = reader.ReadBytes(len);
                result.Add(new BigInteger(bytes));
            }
        }
        catch
        {
            // Если не удалось распарсить — вернём что есть
        }
        return result;
    }

    private byte[] SerializeBigIntegers(List<BigInteger> values)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(values.Count);
        foreach (var val in values)
        {
            var bytes = val.ToByteArray();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        return ms.ToArray();
    }

    [RelayCommand]
    private async Task ExportPrivateKeyAsync()
    {
        if (PrivateKeyList == null)
        {
            StatusMessage = "Сначала сгенерируйте ключи";
            return;
        }

        // Формируем содержимое файла
        var lines = new List<string>
    {
        "ALGORITHM=Knapsack",
        "TYPE=PRIVATE",
        $"LENGTH={SequenceLength}",
        $"MULTIPLIER={Multiplier}",
        $"MODULUS={Modulus}",
        // Приватная последовательность может быть очень длинной, но текстовый формат это выдержит
        $"PRIVATE_SEQ={string.Join(",", PrivateKeyList.Select(x => x.ToString()))}"
    };
        var content = string.Join("\n", lines);

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Private Key File", new List<string> { ".txt" });
        picker.SuggestedFileName = "private_key";

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

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

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var content = await FileIO.ReadTextAsync(file);
                var lines = content.Split('\n').Select(l => l.Trim()).ToList();

                // Валидация заголовков
                bool isKnapsack = lines.Any(l => l.StartsWith("ALGORITHM=Knapsack"));
                bool isPrivate = lines.Any(l => l.StartsWith("TYPE=PRIVATE"));

                if (!isKnapsack || !isPrivate)
                    throw new Exception("Неверный формат файла ключа");

                // Парсинг параметров
                Multiplier = BigInteger.Parse(lines.First(l => l.StartsWith("MULTIPLIER=")).Split('=')[1]);
                Modulus = BigInteger.Parse(lines.First(l => l.StartsWith("MODULUS=")).Split('=')[1]);
                SequenceLength = int.Parse(lines.First(l => l.StartsWith("LENGTH=")).Split('=')[1]);

                // Парсинг приватной последовательности
                PrivateKeyList = lines.First(l => l.StartsWith("PRIVATE_SEQ="))
                    .Split('=')[1]
                    .Split(',')
                    .Select(BigInteger.Parse)
                    .ToList();

                SuperincreasingSequence = $"Загружен: {PrivateKeyList.Count} элементов";
                StatusMessage = "Приватный ключ импортирован. Готов к расшифровке.";
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

        // 🔥 Вместо true/false
        FrequencyPanelVisibility = Visibility.Visible;
        StatusMessage = $"Анализ выполнен: {FrequencyStats.UniqueBlocks} уникальных блоков";
    }

    [RelayCommand]
    private void CloseFrequencyPanel()
    {
        // 🔥 Вместо false
        FrequencyPanelVisibility = Visibility.Collapsed;
    }


}