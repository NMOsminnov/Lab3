using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Lab3.Services;

public static class HammingService
{
    private static readonly ILogService _log = App.Services?.GetService<ILogService>()!;

    // 🔹 Систематический код (7,4): [d0 d1 d2 d3 p0 p1 p2]
    // Данные в битах 0-3, проверочные в 4-6
    // p0 = d0 ^ d1 ^ d3
    // p1 = d0 ^ d2 ^ d3
    // p2 = d1 ^ d2 ^ d3

    public static byte[] Encode(byte[] data)
    {
        _log?.LogDebug($"[Hamming] Encode: input={data.Length} bytes");
        var result = new List<byte>(data.Length * 2);

        foreach (var b in data)
        {
            // Низкий ниббл
            result.Add(EncodeNibble(b & 0xF));
            // Высокий ниббл
            result.Add(EncodeNibble((b >> 4) & 0xF));
        }

        _log?.LogDebug($"[Hamming] Encode complete: {data.Length} → {result.Count} bytes");
        return result.ToArray();
    }

    public static byte[] Decode(byte[] encoded)
    {
        _log?.LogDebug($"[Hamming] Decode: input={encoded.Length} bytes");
        var result = new List<byte>(encoded.Length / 2);
        int errorsFixed = 0;

        for (int i = 0; i < encoded.Length; i += 2)
        {
            if (i + 1 >= encoded.Length) break;

            int low = DecodeNibble(encoded[i], ref errorsFixed);
            int high = DecodeNibble(encoded[i + 1], ref errorsFixed);

            result.Add((byte)((high << 4) | low));
        }

        _log?.LogDebug($"[Hamming] Decode complete: {encoded.Length} → {result.Count} bytes, errors fixed: {errorsFixed}");
        return result.ToArray();
    }

    private static byte EncodeNibble(int nibble)
    {
        int d0 = (nibble >> 0) & 1;
        int d1 = (nibble >> 1) & 1;
        int d2 = (nibble >> 2) & 1;
        int d3 = (nibble >> 3) & 1;

        int p0 = d0 ^ d1 ^ d3;
        int p1 = d0 ^ d2 ^ d3;
        int p2 = d1 ^ d2 ^ d3;

        // Упаковка: [d0 d1 d2 d3 p0 p1 p2]
        return (byte)(d0 | (d1 << 1) | (d2 << 2) | (d3 << 3) | (p0 << 4) | (p1 << 5) | (p2 << 6));
    }

    private static int DecodeNibble(byte encoded, ref int errorsFixed)
    {
        int d0 = (encoded >> 0) & 1;
        int d1 = (encoded >> 1) & 1;
        int d2 = (encoded >> 2) & 1;
        int d3 = (encoded >> 3) & 1;
        int p0 = (encoded >> 4) & 1;
        int p1 = (encoded >> 5) & 1;
        int p2 = (encoded >> 6) & 1;

        // Вычисление синдрома
        int s0 = p0 ^ d0 ^ d1 ^ d3;
        int s1 = p1 ^ d0 ^ d2 ^ d3;
        int s2 = p2 ^ d1 ^ d2 ^ d3;

        int syndrome = (s2 << 2) | (s1 << 1) | s0;

        if (syndrome != 0)
        {
            // Таблица: синдром → позиция бита для инверсии (0..6)
            // Выведена из структуры проверочных уравнений
            int[] fixPos = { -1, 4, 5, 0, 6, 1, 2, 3 };
            int pos = fixPos[syndrome];

            if (pos >= 0 && pos < 7)
            {
                encoded ^= (byte)(1 << pos);
                errorsFixed++;
                _log?.LogDebug($"[Hamming] Corrected bit {pos} (syndrome={syndrome})");

                // Перечитываем данные после исправления
                d0 = (encoded >> 0) & 1;
                d1 = (encoded >> 1) & 1;
                d2 = (encoded >> 2) & 1;
                d3 = (encoded >> 3) & 1;
            }
        }

        return d0 | (d1 << 1) | (d2 << 2) | (d3 << 3);
    }
}