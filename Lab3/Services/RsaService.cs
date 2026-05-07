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
    Task<CryptoResult> EncryptAsync(byte[] data, RsaKeys publicKey, RsaKeys? privateKeyForSigning = null);
    Task<CryptoResult> DecryptAsync(byte[] cipher, RsaKeys privateKey, RsaKeys? publicKeyForVerification = null);
    Task<CryptoResult> SignAsync(byte[] message, RsaKeys privateKey);
    Task<CryptoResult> VerifySignatureAsync(byte[] message, byte[] signature, RsaKeys publicKey);
}

public class RsaService : IRsaService
{
    private readonly ILogService _log;
    private readonly Random _random = new();

    public RsaService(ILogService log) => _log = log;

    public Task<CryptoResult> GenerateKeysAsync(int minPrime = 50, int maxPrime = 200)
    {
        try
        {
            _log.LogDebug("[RSA] Generating keys...");
            int p = GetRandomPrime(minPrime, maxPrime);
            int q = GetRandomPrime(minPrime, maxPrime);
            while (q == p) q = GetRandomPrime(minPrime, maxPrime);

            BigInteger n = (BigInteger)p * q;
            BigInteger phi = (BigInteger)(p - 1) * (q - 1);

            BigInteger e = new BigInteger(65537);
            if (GCD(e, phi) != 1) e = FindCoprime(phi, 3);

            BigInteger d = ModularInverse(e, phi);

            var keys = new RsaKeys { E = e, N = n, D = d, PrimeP = p, PrimeQ = q };
            _log.LogInfo($"[RSA] Keys generated: p={p}, q={q}, n bits={GetBitLength(n)}");

            return Task.FromResult(CryptoResult.Ok($"p={p}, q={q}\nn={n}\ne={e}", keys));
        }
        catch (Exception ex)
        {
            _log.LogError($"[RSA] KeyGen error: {ex.Message}");
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    public async Task<CryptoResult> EncryptAsync(byte[] data, RsaKeys publicKey, RsaKeys? privateKeyForSigning = null)
    {
        try
        {
            _log.LogDebug($"[RSA-Enc] Input: {data.Length} bytes");

            // 🔹 Шаг 1: Подпись
            byte[] signature = Array.Empty<byte>();
            if (privateKeyForSigning != null && privateKeyForSigning.D != 0)
            {
                _log.LogDebug("[RSA-Enc] Creating signature...");
                var signResult = await SignAsync(data, privateKeyForSigning);
                if (!signResult.Success) return signResult;
                signature = signResult.BinaryData!;
                _log.LogDebug($"[RSA-Enc] Signature created: {signature.Length} bytes");
            }

            // 🔹 Шаг 2: Упаковка
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(signature.Length);
            writer.Write(signature);
            writer.Write(data.Length);
            writer.Write(data);

            byte[] payload = ms.ToArray();
            _log.LogDebug($"[RSA-Enc] Payload: sigLen={signature.Length}, dataLen={data.Length}, total={payload.Length} bytes");

            // 🔹 Шаг 3: Шифрование
            int bitLength = GetBitLength(publicKey.N);
            int blockSize = (int)((bitLength - 1) / 8);
            if (blockSize < 1) blockSize = 1;
            _log.LogDebug($"[RSA-Enc] RSA params: N bits={bitLength}, blockSize={blockSize}");

            var cipherBlocks = new List<BigInteger>();
            for (int i = 0; i < payload.Length; i += blockSize)
            {
                int len = Math.Min(blockSize, payload.Length - i);
                var blockBytes = new byte[len];
                Array.Copy(payload, i, blockBytes, 0, len);

                var m = new BigInteger(blockBytes, isUnsigned: true, isBigEndian: true);
                var c = BigInteger.ModPow(m, publicKey.E, publicKey.N);
                cipherBlocks.Add(c);
            }

            _log.LogDebug($"[RSA-Enc] Encrypted {cipherBlocks.Count} blocks");
            return CryptoResult.Ok($"Зашифровано (с подписью): {cipherBlocks.Count} блоков", SerializeBigIntegers(cipherBlocks));
        }
        catch (Exception ex)
        {
            _log.LogError($"[RSA-Enc] Error: {ex.Message}\n{ex.StackTrace}");
            return CryptoResult.Error(ex.Message);
        }
    }

    public async Task<CryptoResult> DecryptAsync(byte[] cipher, RsaKeys privateKey, RsaKeys? publicKeyForVerification = null)
    {
        try
        {
            _log.LogDebug($"[RSA-Dec] Input cipher: {cipher.Length} bytes");
            var blocks = DeserializeBigIntegers(cipher);
            _log.LogDebug($"[RSA-Dec] Deserialized {blocks.Count} BigInteger blocks");

            var decrypted = new List<byte>();
            int bitLength = GetBitLength(privateKey.N);
            int blockSize = (int)((bitLength - 1) / 8);
            if (blockSize < 1) blockSize = 1;

            _log.LogDebug($"[RSA-Dec] RSA params: N bits={bitLength}, blockSize={blockSize}");

            foreach (var c in blocks)
            {
                var m = BigInteger.ModPow(c, privateKey.D, privateKey.N);
                var bytes = m.ToByteArray(isUnsigned: true, isBigEndian: true);

                // 🔹 Критично: обрезаем до размера блока, чтобы убрать лишний байт знака
                if (bytes.Length > blockSize)
                    bytes = bytes.Skip(bytes.Length - blockSize).ToArray();
                else if (bytes.Length < blockSize)
                    Array.Resize(ref bytes, blockSize);

                decrypted.AddRange(bytes);
            }

            byte[] payload = decrypted.ToArray();
            _log.LogDebug($"[RSA-Dec] Decrypted payload: {payload.Length} bytes");
            LogBytesPreview("[RSA-Dec] Payload preview", payload, 30);

            // 🔹 Шаг 1: Разбор структуры
            using var ms = new MemoryStream(payload);
            using var reader = new BinaryReader(ms);

            if (ms.Length < 8) // Минимум: 4 байта длины подписи + 4 байта длины данных
            {
                _log.LogError($"[RSA-Dec] Payload too short: {payload.Length} bytes, need at least 8");
                return CryptoResult.Error("❌ Ошибка формата: данные повреждены");
            }

            int sigLen = reader.ReadInt32();
            _log.LogDebug($"[RSA-Dec] Read signature length: {sigLen}");

            if (sigLen < 0 || sigLen > payload.Length)
            {
                _log.LogError($"[RSA-Dec] Invalid signature length: {sigLen}");
                return CryptoResult.Error("❌ Ошибка формата: неверная длина подписи");
            }

            byte[] signature = reader.ReadBytes(sigLen);
            _log.LogDebug($"[RSA-Dec] Read signature: {signature.Length} bytes");

            if (ms.Position + 4 > ms.Length)
            {
                _log.LogError($"[RSA-Dec] Not enough data for data length field");
                return CryptoResult.Error("❌ Ошибка формата: обрезанные данные");
            }

            int dataLen = reader.ReadInt32();
            _log.LogDebug($"[RSA-Dec] Read data length: {dataLen}");

            if (dataLen < 0 || dataLen > payload.Length)
            {
                _log.LogError($"[RSA-Dec] Invalid data length: {dataLen}");
                return CryptoResult.Error("❌ Ошибка формата: неверная длина данных");
            }

            if (ms.Position + dataLen > ms.Length)
            {
                _log.LogError($"[RSA-Dec] Not enough data: need {dataLen}, have {ms.Length - ms.Position}");
                return CryptoResult.Error("❌ Ошибка формата: данные обрезаны");
            }

            byte[] data = reader.ReadBytes(dataLen);
            _log.LogDebug($"[RSA-Dec] Read data: {data.Length} bytes");
            LogBytesPreview("[RSA-Dec] Data preview", data, 30);

            // 🔹 Шаг 2: Проверка подписи
            if (publicKeyForVerification != null && publicKeyForVerification.E != 0 && signature.Length > 0)
            {
                _log.LogDebug("[RSA-Dec] Verifying signature...");
                var verifyResult = await VerifySignatureAsync(data, signature, publicKeyForVerification);

                if (!verifyResult.Success)
                {
                    _log.LogError($"[RSA-Dec] Verify failed: {verifyResult.ErrorMessage}");
                    return CryptoResult.Error("❌ Подпись не верна! Данные могли быть изменены.");
                }

                if (verifyResult.BinaryData?[0] != 1)
                {
                    _log.LogError("[RSA-Dec] Signature verification returned invalid");
                    return CryptoResult.Error("❌ Подпись НЕВЕРНА! Данные изменены.");
                }
                _log.LogInfo("[RSA-Dec] Signature verified OK");
            }

            _log.LogInfo($"[RSA-Dec] Success: decrypted {data.Length} bytes");
            return CryptoResult.Ok("Расшифровано и проверено", data);
        }
        catch (Exception ex)
        {
            _log.LogError($"[RSA-Dec] Error: {ex.Message}\n{ex.StackTrace}");
            return CryptoResult.Error(ex.Message);
        }
    }

    // 🔹 Цифровая подпись с усечением хэша под размер модуля
    public Task<CryptoResult> SignAsync(byte[] message, RsaKeys privateKey)
    {
        try
        {
            // 1. Вычисляем полный хэш
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] fullHash = sha256.ComputeHash(message);

            // 2. Вычисляем, сколько байт хэша поместится в модуль N
            int nBits = GetBitLength(privateKey.N);
            int maxHashBytes = Math.Max(1, (nBits - 1) / 8); // -1 для запаса, чтобы значение точно было < N
            if (maxHashBytes > fullHash.Length) maxHashBytes = fullHash.Length;

            // 3. Усекаем хэш
            byte[] hash = new byte[maxHashBytes];
            Array.Copy(fullHash, 0, hash, 0, maxHashBytes);

            _log.LogDebug($"[RSA-Sign] Using truncated hash: {hash.Length} bytes (of {fullHash.Length}), N bits={nBits}");

            // 4. Подписываем
            var hashBigInt = new BigInteger(hash, isUnsigned: true, isBigEndian: true);
            var signature = BigInteger.ModPow(hashBigInt, privateKey.D, privateKey.N);

            return Task.FromResult(CryptoResult.Ok(
                "Подпись создана",
                signature.ToByteArray(isUnsigned: true, isBigEndian: true)));
        }
        catch (Exception ex)
        {
            _log.LogError($"[RSA-Sign] Error: {ex.Message}");
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    // 🔹 Проверка подписи с усечением
    public Task<CryptoResult> VerifySignatureAsync(byte[] message, byte[] signatureBytes, RsaKeys publicKey)
    {
        try
        {
            // 1. Хэшируем сообщение
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] fullHash = sha256.ComputeHash(message);

            // 2. Усекаем так же, как при подписи
            int nBits = GetBitLength(publicKey.N);
            int maxHashBytes = Math.Max(1, (nBits - 1) / 8);
            if (maxHashBytes > fullHash.Length) maxHashBytes = fullHash.Length;

            byte[] messageHash = new byte[maxHashBytes];
            Array.Copy(fullHash, 0, messageHash, 0, maxHashBytes);
            var messageHashBigInt = new BigInteger(messageHash, isUnsigned: true, isBigEndian: true);

            // 3. "Расшифровываем" подпись
            var signatureBigInt = new BigInteger(signatureBytes, isUnsigned: true, isBigEndian: true);
            var decryptedHash = BigInteger.ModPow(signatureBigInt, publicKey.E, publicKey.N);
            var decryptedHashBytes = decryptedHash.ToByteArray(isUnsigned: true, isBigEndian: true);

            // 4. Сравниваем только значимые байты
            _log.LogDebug($"[RSA-Verify] Comparing {maxHashBytes} bytes");
            _log.LogDebug($"[RSA-Verify] Original:  {BitConverter.ToString(messageHash).Replace("-", " ")}");
            _log.LogDebug($"[RSA-Verify] Decrypted:{BitConverter.ToString(decryptedHashBytes).Replace("-", " ")}");

            bool isValid = messageHashBigInt == decryptedHash;

            return Task.FromResult(CryptoResult.Ok(
                isValid ? "Подпись верна" : "Подпись НЕВЕРНА",
                new[] { isValid ? (byte)1 : (byte)0 }));
        }
        catch (Exception ex)
        {
            _log.LogError($"[RSA-Verify] Error: {ex.Message}");
            return Task.FromResult(CryptoResult.Error(ex.Message));
        }
    }

    // 🔹 Вспомогательные методы
    private int GetRandomPrime(int min, int max)
    {
        var primes = Enumerable.Range(min, max - min + 1).Where(IsPrime).ToList();
        return primes[_random.Next(primes.Count)];
    }

    private bool IsPrime(int n)
    {
        if (n < 2) return false;
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
            BigInteger t = m; m = a % m; a = t;
            t = x0; x0 = x1 - q * x0; x1 = t;
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

    private int GetBitLength(BigInteger value)
    {
        if (value == 0) return 1;
        int bits = 0;
        BigInteger absValue = value.Sign < 0 ? -value : value;
        while (absValue > 0) { bits++; absValue >>= 1; }
        return bits;
    }

    private void LogBytesPreview(string prefix, byte[] bytes, int maxLen)
    {
        if (_log == null) return;
        int len = Math.Min(bytes.Length, maxLen);
        var preview = BitConverter.ToString(bytes.Take(len).ToArray()).Replace("-", " ");
        _log.LogDebug($"{prefix}: [{len}/{bytes.Length}] {preview}{(bytes.Length > maxLen ? "..." : "")}");
    }
}