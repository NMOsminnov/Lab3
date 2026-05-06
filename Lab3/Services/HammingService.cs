using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Lab3.Services;

public static class HammingService
{
    private static readonly ILogService _log = App.Services?.GetService<ILogService>()!;

    private static readonly int[,] GeneratorMatrix = {
        {1,0,0,0,1,1,0},
        {0,1,0,0,1,0,1},
        {0,0,1,0,0,1,1},
        {0,0,0,1,1,1,1}
    };

    private static readonly int[,] ParityCheck = {
        {1,0,1,0,1,0,1},
        {0,1,1,0,0,1,1},
        {0,0,0,1,1,1,1}
    };

    public static byte[] Encode(byte[] data)
    {
        _log?.LogDebug($"[Hamming] Encode: input={data.Length} bytes");

        var result = new List<byte>();
        foreach (var b in data)
        {
            for (int half = 0; half < 2; half++)
            {
                int nibble = (b >> (half * 4)) & 0xF;
                int encoded = EncodeNibble(nibble);
                result.Add((byte)encoded);
            }
        }

        _log?.LogDebug($"[Hamming] Encode complete: {data.Length} → {result.Count} bytes");
        return result.ToArray();
    }

    public static byte[] Decode(byte[] encoded)
    {
        _log?.LogDebug($"[Hamming] Decode: input={encoded.Length} bytes");

        var result = new List<byte>();
        int errorsFixed = 0;

        for (int i = 0; i < encoded.Length; i += 2)
        {
            if (i + 1 >= encoded.Length) break;

            int e1 = encoded[i];
            int e2 = encoded[i + 1];

            int nibble1 = DecodeNibble(e1, ref errorsFixed);
            int nibble2 = DecodeNibble(e2, ref errorsFixed);

            result.Add((byte)((nibble2 << 4) | nibble1));
        }

        _log?.LogDebug($"[Hamming] Decode complete: {encoded.Length} → {result.Count} bytes, errors fixed: {errorsFixed}");
        return result.ToArray();
    }

    private static int EncodeNibble(int nibble)
    {
        int result = 0;
        for (int row = 0; row < 4; row++)
        {
            if (((nibble >> row) & 1) != 0)
            {
                for (int col = 0; col < 7; col++)
                {
                    if (GeneratorMatrix[row, col] != 0)
                        result |= (1 << col);
                }
            }
        }
        return result;
    }

    private static int DecodeNibble(int encoded, ref int errorsFixed)
    {
        int s0 = 0, s1 = 0, s2 = 0;
        for (int col = 0; col < 7; col++)
        {
            int bit = (encoded >> col) & 1;
            s0 ^= ParityCheck[0, col] * bit;
            s1 ^= ParityCheck[1, col] * bit;
            s2 ^= ParityCheck[2, col] * bit;
        }
        int syndrome = (s2 << 2) | (s1 << 1) | s0;

        if (syndrome != 0 && syndrome <= 7)
        {
            encoded ^= (1 << (syndrome - 1));
            errorsFixed++;
            _log?.LogDebug($"[Hamming] Corrected bit {syndrome}");
        }

        int nibble = 0;
        nibble |= ((encoded >> 2) & 1) << 0;
        nibble |= ((encoded >> 4) & 1) << 1;
        nibble |= ((encoded >> 5) & 1) << 2;
        nibble |= ((encoded >> 6) & 1) << 3;

        return nibble;
    }
}