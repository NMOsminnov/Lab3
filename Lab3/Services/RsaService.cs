using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Lab3.Models;

namespace Lab3.Services;

public interface IRsaService
{
    Task<CryptoResult> GenerateKeysAsync(int minPrime = 50, int maxPrime = 200);
    Task<CryptoResult> EncryptAsync(byte[] data, RsaKeys publicKey);
    Task<CryptoResult> DecryptAsync(byte[] cipher, RsaKeys privateKey);
}

public class RsaService : IRsaService
{
    private readonly Random _random = new();

    public Task<CryptoResult> GenerateKeysAsync(int minPrime = 50, int maxPrime = 200)
    {
        try
        {
            int p = GetRandomPrime(minPrime, maxPrime);
            int q = GetRandomPrime(minPrime, maxPrime);
            while (q == p) q = GetRandomPrime(minPrime, maxPrime);

            BigInteger n = (BigInteger)p * q;
            BigInteger phi = (BigInteger)(p - 1) * (q - 1);

            BigInteger e = new BigInteger(65537);
            if (GCD(e, phi) != 1) e = FindCoprime(phi, 3);

            BigInteger d = ModularInverse(e, phi);

            var keys = new RsaKeys
            {
                E = e,
                N = n,
                D = d,
                PrimeP = p,
                PrimeQ = q
            };

            return Task.FromResult(CryptoResult.Ok(
                $"p={p}, q={q}\nn={n}\ne={e}",
                keys));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    public Task<CryptoResult> EncryptAsync(byte[] data, RsaKeys publicKey)
    {
        try
        {
            int bitLength = GetBitLength(publicKey.N);
            // 🔥 Явное приведение: деление BigInteger на целое даёт long, приводим к int
            int blockSize = (int)((bitLength - 1) / 8);
            if (blockSize < 1) blockSize = 1;

            var cipherBlocks = new List<BigInteger>();

            for (int i = 0; i < data.Length; i += blockSize)
            {
                int len = Math.Min(blockSize, data.Length - i);
                var blockBytes = new byte[len];
                Array.Copy(data, i, blockBytes, 0, len);

                var m = new BigInteger(blockBytes, isUnsigned: true, isBigEndian: true);
                var c = BigInteger.ModPow(m, publicKey.E, publicKey.N);
                cipherBlocks.Add(c);
            }

            return Task.FromResult(CryptoResult.Ok(
                $"Зашифровано блоков: {cipherBlocks.Count}",
                SerializeBigIntegers(cipherBlocks)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    public Task<CryptoResult> DecryptAsync(byte[] cipher, RsaKeys privateKey)
    {
        try
        {
            var blocks = DeserializeBigIntegers(cipher);
            var decrypted = new List<byte>();

            int bitLength = GetBitLength(privateKey.N);
            int blockSize = (int)((bitLength - 1) / 8);
            if (blockSize < 1) blockSize = 1;

            foreach (var c in blocks)
            {
                var m = BigInteger.ModPow(c, privateKey.D, privateKey.N);
                var bytes = m.ToByteArray(isUnsigned: true, isBigEndian: true);

                // 🔥 Гарантируем размер блока
                if (bytes.Length < blockSize)
                {
                    Array.Resize(ref bytes, blockSize);
                }
                decrypted.AddRange(bytes.Take(blockSize));
            }

            return Task.FromResult(CryptoResult.Ok(
                "Расшифровано",
                decrypted.ToArray()));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    // 🔥 Вспомогательные методы — все приведения явные
    private int GetRandomPrime(int min, int max)
    {
        var primes = Enumerable.Range(min, max - min + 1).Where(IsPrime).ToList();
        return primes[_random.Next(primes.Count)];
    }

    private bool IsPrime(int n)
    {
        if (n < 2) return false;
        // 🔥 Math.Sqrt возвращает double, явно приводим к int
        int limit = (int)Math.Sqrt((double)n);
        for (int i = 2; i <= limit; i++)
            if (n % i == 0) return false;
        return true;
    }

    private BigInteger GCD(BigInteger a, BigInteger b) => b == 0 ? a : GCD(b, a % b);

    private BigInteger FindCoprime(BigInteger phi, BigInteger start)
    {
        BigInteger e = start;
        while (GCD(e, phi) != 1) e++;
        return e;
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

    // 🔥 Вынесенный метод для получения длины в битах (без extension, чтобы избежать конфликтов)
    private int GetBitLength(BigInteger value)
    {
        if (value == 0) return 1;
        int bits = 0;
        BigInteger absValue = value.Sign < 0 ? -value : value;
        while (absValue > 0)
        {
            bits++;
            absValue >>= 1;
        }
        return bits;
    }
}