using Lab3.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lab3.Services;

public class FrequencyAnalyzer
{
    /// <summary>
    /// Анализирует частоту зашифрованных блоков (чисел)
    /// </summary>
    public List<FrequencyItem> Analyze(string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
            return new List<FrequencyItem>();

        var blocks = encryptedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        if (blocks.Count == 0)
            return new List<FrequencyItem>();

        var frequency = blocks
            .GroupBy(x => x)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Value)
            .ToList();

        int maxCount = frequency.Max(x => x.Count);

        return frequency
            .Take(50) // 🔥 Ограничиваем топ-50 для производительности графика
            .Select(x => new FrequencyItem
            {
                BlockValue = x.Value,
                Count = x.Count,
                Percentage = (double)x.Count / blocks.Count * 100,
                BarHeight = Math.Max(2, (double)x.Count / maxCount * 200)
            })
            .ToList();
    }
    /// <summary>
    /// Возвращает сводную статистику
    /// </summary>
    public FrequencyStats GetStats(string encryptedText)
    {
        var items = Analyze(encryptedText);
        int totalBlocks = items.Sum(x => x.Count);
        int uniqueBlocks = items.Count;
        int maxFrequency = items.Max(x => x.Count);
        double avgFrequency = totalBlocks > 0 ? (double)totalBlocks / uniqueBlocks : 0;

        return new FrequencyStats
        {
            TotalBlocks = totalBlocks,
            UniqueBlocks = uniqueBlocks,
            MaxFrequency = maxFrequency,
            AverageFrequency = avgFrequency,
            EntropyEstimate = CalculateEntropy(items, totalBlocks)
        };
    }

    private double CalculateEntropy(List<FrequencyItem> items, int total)
    {
        if (total == 0) return 0;
        double entropy = 0;
        foreach (var item in items)
        {
            double p = (double)item.Count / total;
            if (p > 0) entropy -= p * Math.Log2(p);
        }
        return entropy;
    }
}



public class FrequencyStats
{
    public int TotalBlocks { get; set; }
    public int UniqueBlocks { get; set; }
    public int MaxFrequency { get; set; }
    public double AverageFrequency { get; set; }
    public double EntropyEstimate { get; set; }

    // ✅ Готовые строки для привязки
    public string EntropyFormatted => $"{EntropyEstimate:F2}";
}