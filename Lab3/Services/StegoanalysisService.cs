using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Lab3.Models;

namespace Lab3.Services;

public class SteganalysisService
{
    private readonly ILogService _log;

    public SteganalysisService(ILogService log) => _log = log;

    /// <summary>
    /// χ²-тест для обнаружения стеганографических внедрений
    /// </summary>
    public async Task<SteganalysisResult> AnalyzeImageAsync(
        string imagePath,
        int bitsPerChannel,
        double threshold = 25.0)
    {
        _log.LogDebug($"[Analysis] Analyzing: {imagePath}, bits={bitsPerChannel}, threshold={threshold}");

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixels = (await decoder.GetPixelDataAsync()).DetachPixelData();

            int width = (int)decoder.PixelWidth;
            int height = (int)decoder.PixelHeight;
            int totalPixels = width * height;

            //  Считаем частоты пар значений (PoV-анализ)
            // Для каждого канала: считаем, как часто встречаются пары (2k, 2k+1)
            var pairs = new Dictionary<int, (int even, int odd)>();

            for (int p = 0; p < totalPixels; p++)
            {
                int offset = p * 4; // BGRA
                for (int ch = 0; ch < 3; ch++) // R, G, B
                {
                    byte val = pixels[offset + ch];
                    int bucket = val >> (8 - bitsPerChannel); // Группируем по старшим битам для стабильности

                    if (!pairs.ContainsKey(bucket))
                        pairs[bucket] = (0, 0);

                    if ((val & 1) == 0)
                        pairs[bucket] = (pairs[bucket].even + 1, pairs[bucket].odd);
                    else
                        pairs[bucket] = (pairs[bucket].even, pairs[bucket].odd + 1);
                }
            }

            //  Вычисляем χ²
            double chiSquare = 0;
            int totalPairs = 0;

            foreach (var (_, counts) in pairs)
            {
                int even = counts.even;
                int odd = counts.odd;
                int total = even + odd;
                totalPairs += total;

                if (total > 0)
                {
                    double expected = total / 2.0;
                    chiSquare += Math.Pow(even - expected, 2) / expected;
                    chiSquare += Math.Pow(odd - expected, 2) / expected;
                }
            }

            //  Упрощённая оценка p-value (эмпирическая)
            // Для реального использования нужна таблица χ²-распределения
            double pValue = 1.0 / (1.0 + chiSquare / (2.0 * pairs.Count));
            bool isDetected = chiSquare > threshold;

            _log.LogDebug($"[Analysis] χ²={chiSquare:F2}, p={pValue:F4}, detected={isDetected}");

            return new SteganalysisResult
            {
                ImageName = file.Name,
                BitsPerChannel = bitsPerChannel,
                FillPercentage = 100, // Заполнение вычисляется отдельно
                ChiSquare = chiSquare,
                PValue = pValue,
                IsDetected = isDetected,
                Notes = $"Pixels: {totalPixels}, Pairs: {pairs.Count}"
            };
        }
        catch (Exception ex)
        {
            _log.LogError($"[Analysis] Failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Автоматическое исследование: 2 изображения × 5 уровней заполнения × 3 варианта бит
    /// </summary>
    public async Task<List<SteganalysisReport>> RunFullStudyAsync(
        List<string> imagePaths,
        List<int> bitOptions,
        List<double> fillLevels,
        double threshold = 25.0)
    {
        _log.LogInfo($"[Study] Starting: {imagePaths.Count} images, bits={string.Join(",", bitOptions)}, fills={string.Join(",", fillLevels)}%");

        var reports = new List<SteganalysisReport>();

        foreach (int bits in bitOptions)
        {
            var report = new SteganalysisReport
            {
                BitsPerChannel = bits,
                Threshold = threshold
            };

            foreach (double fill in fillLevels)
            {
                int detectedCount = 0;
                int totalTests = 0;

                foreach (var path in imagePaths)
                {
                    //  Создаём тестовое изображение с заданным уровнем заполнения
                    var result = await AnalyzeWithFillAsync(path, bits, fill / 100.0, threshold);

                    totalTests++;
                    if (result.IsDetected) detectedCount++;

                    _log.LogDebug($"[Study] {path} @ {fill}% fill, {bits} bits: χ²={result.ChiSquare:F2}, detected={result.IsDetected}");
                }

                double detectionRate = totalTests > 0 ? (double)detectedCount / totalTests * 100 : 0;

                report.Points.Add(new SteganalysisPoint
                {
                    FillPercentage = fill,
                    DetectionRate = detectionRate,
                    TotalTests = totalTests,
                    DetectedCount = detectedCount
                });
            }

            reports.Add(report);
            _log.LogInfo($"[Study] Bits={bits}: completed {report.Points.Count} points");
        }

        return reports;
    }

    /// <summary>
    /// Анализ с искусственным заполнением контейнера
    /// </summary>
    private async Task<SteganalysisResult> AnalyzeWithFillAsync(
    string imagePath,
    int bitsPerChannel,
    double fillRatio,
    double threshold)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var pixels = (await decoder.GetPixelDataAsync()).DetachPixelData();

        int width = (int)decoder.PixelWidth;
        int height = (int)decoder.PixelHeight;
        int totalPixels = width * height;
        int bitsPerPixel = bitsPerChannel * 3;

        // 🔹 ИСПРАВЛЕНИЕ: вычисляем сколько бит нужно внедрить
        int totalBitsAvailable = totalPixels * bitsPerPixel;
        int bitsToEmbed = (int)(totalBitsAvailable * fillRatio);

        // Генерируем достаточно случайных данных
        int bytesNeeded = bitsToEmbed / 8 + 1000; // +запас
        var random = new Random(42);
        var testData = new byte[bytesNeeded];
        random.NextBytes(testData);

        // 🔹 Внедряем данные
        int bitIdx = 0;
        int totalBitsEmbedded = 0;

        foreach (byte byteVal in testData)
        {
            for (int pos = 7; pos >= 0; pos--)
            {
                if (bitIdx >= bitsToEmbed) break;

                int pixelIndex = bitIdx / bitsPerPixel;
                if (pixelIndex >= totalPixels) break;

                int channelIndex = (bitIdx % bitsPerPixel) / bitsPerChannel;
                int bitPosition = (bitIdx % bitsPerPixel) % bitsPerChannel;

                int offset = pixelIndex * 4;
                byte original = pixels[offset + channelIndex];
                int bit = (byteVal >> pos) & 1;

                int mask = ~((1 << bitsPerChannel) - 1);
                pixels[offset + channelIndex] = (byte)(
                    (original & mask) |
                    (bit << (bitsPerChannel - 1 - bitPosition))
                );

                bitIdx++;
                totalBitsEmbedded++;
            }

            if (bitIdx >= bitsToEmbed) break;
        }

        _log.LogDebug($"[Fill] Embedded {totalBitsEmbedded} bits ({totalBitsEmbedded / 8} bytes) into {totalPixels} pixels, fillRatio={fillRatio:P1}, bitsPerPixel={bitsPerPixel}");

        // 🔹 Анализируем
        var pairs = new Dictionary<int, (int even, int odd)>();

        for (int p = 0; p < totalPixels; p++)
        {
            int offset = p * 4;
            for (int ch = 0; ch < 3; ch++)
            {
                byte val = pixels[offset + ch];
                int bucket = val >> (8 - bitsPerChannel);

                if (!pairs.ContainsKey(bucket))
                    pairs[bucket] = (0, 0);

                if ((val & 1) == 0)
                    pairs[bucket] = (pairs[bucket].even + 1, pairs[bucket].odd);
                else
                    pairs[bucket] = (pairs[bucket].even, pairs[bucket].odd + 1);
            }
        }

        double chiSquare = 0;
        foreach (var (_, counts) in pairs)
        {
            int even = counts.even;
            int odd = counts.odd;
            int total = even + odd;
            if (total > 0)
            {
                double expected = total / 2.0;
                chiSquare += Math.Pow(even - expected, 2) / expected;
                chiSquare += Math.Pow(odd - expected, 2) / expected;
            }
        }

        double pValue = 1.0 / (1.0 + chiSquare / (2.0 * pairs.Count));
        bool isDetected = chiSquare > threshold;

        _log.LogDebug($"[Analysis] χ²={chiSquare:F2}, detected={isDetected}, pairs={pairs.Count}");

        return new SteganalysisResult
        {
            ImageName = file.Name,
            BitsPerChannel = bitsPerChannel,
            FillPercentage = fillRatio * 100,
            ChiSquare = chiSquare,
            PValue = pValue,
            IsDetected = isDetected
        };
    }
}