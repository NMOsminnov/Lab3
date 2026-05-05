using Lab3.Models;
using Lab3.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Lab3.Services;

public class KnapsackService : IKnapsackService
{
    private readonly Random _random = new();

    public List<BigInteger> GenerateSuperincreasingSequence(int length)
    {
        var seq = new List<BigInteger>();

        // Начинаем с небольшого случайного числа
        BigInteger current = _random.Next(100, 1000);

        for (int i = 0; i < length; i++)
        {
            seq.Add(current);
            // 🔥 Гарантируем сверхвозрастание: следующий > суммы всех предыдущих + случайный "зазор"
            BigInteger sum = 0;
            foreach (var val in seq) sum += val;
            current = sum + _random.Next(1, 1000); // зазор побольше для надёжности
        }
        return seq;
    }

    public Task<CryptoResult> GenerateKeysAsync(int length)
    {
        try
        {
            // 🔥 Разрешаем 1-200
            if (length < 1 || length > 200)
                return Task.FromResult(CryptoResult.Error("Длина последовательности: 1-200"));

            var priv = GenerateSuperincreasingSequence(length);

            // 🔥 Считаем сумму через BigInteger
            BigInteger sum = 0;
            foreach (var v in priv) sum += v;

            // 🔥 Модуль > суммы + случайный зазор
            BigInteger modulus = sum + _random.Next(1000, 10000);

            // 🔥 Подбор мультипликатора, взаимно простого с модулем
            BigInteger multiplier;
            do
            {
                // Генерируем случайное число в диапазоне [2, modulus-1]
                multiplier = GenerateRandomBigInteger(2, modulus - 1);
            } while (GCD(multiplier, modulus) != 1);

            // 🔥 Открытый ключ: (priv[i] * w) mod n
            var pub = priv.Select(v => (v * multiplier) % modulus).ToList();

            return Task.FromResult(CryptoResult.Ok(
                $"Private: [{string.Join(", ", priv.Take(10))}... ({length} elements)]\n" +
                $"Public: [{string.Join(", ", pub.Take(10))}... ({length} elements)]\n" +
                $"w={multiplier}, n={modulus}",
                priv, pub, multiplier, modulus));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    // 🔥 Вспомогательный метод для генерации случайного BigInteger в диапазоне
    private BigInteger GenerateRandomBigInteger(BigInteger min, BigInteger max)
    {
        var bytes = max.ToByteArray();
        BigInteger result;
        do
        {
            _random.NextBytes(bytes);
            bytes[bytes.Length - 1] &= 0x7F; // положительное число
            result = new BigInteger(bytes);
        } while (result < min || result > max);
        return result;
    }
    public Task<CryptoResult> EncryptAsync(byte[] data, KnapsackSettings settings)
    {
        try
        {
            if (settings?.PublicKey == null || settings.PublicKey.Count < 1)
                return Task.FromResult(CryptoResult.Error("Открытый ключ не задан"));

            var pub = settings.PublicKey;
            int blockSize = pub.Count; // 200 бит
            int bytesPerBlock = blockSize / 8; // 25 байт

            var cipher = new List<BigInteger>();

            // 🔥 Разбиваем данные на блоки по 25 байт
            for (int offset = 0; offset < data.Length; offset += bytesPerBlock)
            {
                // Берём очередной блок
                int length = Math.Min(bytesPerBlock, data.Length - offset);
                var block = new byte[bytesPerBlock]; // дополняем нулями если нужно
                Array.Copy(data, offset, block, 0, length);

                // 🔥 Шифруем блок: бит=1 → добавляем pub[i]
                BigInteger sum = 0;
                for (int byteIdx = 0; byteIdx < bytesPerBlock; byteIdx++)
                {
                    for (int bitIdx = 0; bitIdx < 8; bitIdx++)
                    {
                        int bitPosition = byteIdx * 8 + bitIdx;
                        if (bitPosition >= blockSize) break;

                        // Проверяем бит (старший бит первого байта → pub[0])
                        if ((block[byteIdx] & (1 << (7 - bitIdx))) != 0)
                        {
                            sum += pub[bitPosition];
                        }
                    }
                }
                cipher.Add(sum);
            }

            // 🔥 Сериализуем результат в байты для сохранения
            var resultBytes = SerializeBigIntegers(cipher);

            return Task.FromResult(CryptoResult.Ok(
                $"Зашифровано блоков: {cipher.Count}",
                resultBytes)); // BinaryData для файла
        }
        catch (Exception ex)
        {
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    // 🔥 Вспомогательный метод: список BigInteger → byte[]
    private byte[] SerializeBigIntegers(List<BigInteger> values)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Записываем количество элементов
        writer.Write(values.Count);

        // Записываем каждое число как байтовый массив с длиной
        foreach (var val in values)
        {
            var bytes = val.ToByteArray();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        return ms.ToArray();
    }
    public Task<CryptoResult> DecryptAsync(byte[] cipherBytes, KnapsackSettings settings)
    {
        try
        {
            if (settings?.SuperincreasingSequence == null || settings.SuperincreasingSequence.Count < 1)
                return Task.FromResult(CryptoResult.Error("Закрытый ключ не задан"));

            // 🔥 Десериализуем входные данные
            var cipher = DeserializeBigIntegers(cipherBytes);

            var priv = settings.SuperincreasingSequence;
            int blockSize = priv.Count;
            int bytesPerBlock = blockSize / 8;

            // 🔥 Вычисляем модульное обратное: w⁻¹ mod n
            BigInteger inv = ModularInverse(settings.Multiplier, settings.Modulus);
            if (inv < 0) inv += settings.Modulus;

            var decrypted = new List<byte>();

            foreach (BigInteger c in cipher)
            {
                // 🔥 Шаг 1: "возвращаем" к сверхвозрастающей последовательности
                BigInteger transformed = (c * inv) % settings.Modulus;
                if (transformed < 0) transformed += settings.Modulus;

                // 🔥 Шаг 2: жадный алгоритм для сверхвозрастающей последовательности
                BigInteger remainder = transformed;
                byte[] blockBytes = new byte[bytesPerBlock];

                // Идём от БОЛЬШОГО к маленькому
                for (int i = blockSize - 1; i >= 0; i--)
                {
                    if (priv[i] <= remainder)
                    {
                        remainder -= priv[i];

                        // Вычисляем позицию бита в байтовом массиве
                        int byteIdx = i / 8;
                        int bitIdx = i % 8;
                        if (byteIdx < bytesPerBlock)
                        {
                            blockBytes[byteIdx] |= (byte)(1 << (7 - bitIdx));
                        }
                    }
                }
                decrypted.AddRange(blockBytes);
            }

            // 🔥 Удаляем паддинг (нули в конце) — можно улучшить, сохраняя длину оригинала
            var result = RemovePadding(decrypted.ToArray());

            return Task.FromResult(CryptoResult.Ok("Расшифровано успешно", result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    // 🔥 Десериализация: byte[] → List<BigInteger>
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
            result.Add(new BigInteger(bytes));
        }
        return result;
    }

    // 🔥 Удаление нулевого паддинга (упрощённо)
    private byte[] RemovePadding(byte[] data)
    {
        int end = data.Length;
        while (end > 0 && data[end - 1] == 0) end--;

        var result = new byte[end];
        Array.Copy(data, result, end);
        return result;
    }
    private BigInteger GCD(BigInteger a, BigInteger b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    private BigInteger ModularInverse(BigInteger a, BigInteger m)
    {
        BigInteger m0 = m, x0 = 0, x1 = 1;
        if (m == 1) return 0;

        while (a > 1)
        {
            if (m == 0) return -1;
            BigInteger q = a / m;
            BigInteger t = m;
            m = a % m;
            a = t;
            t = x0;
            x0 = x1 - q * x0;
            x1 = t;
        }
        return x1 < 0 ? x1 + m0 : x1;
    }
}