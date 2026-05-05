using Lab3.Models;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Lab3.Services;

public interface IKnapsackService
{
    Task<CryptoResult> GenerateKeysAsync(int length);
    // 🔥 Меняем string на byte[] для работы с файлами
    Task<CryptoResult> EncryptAsync(byte[] data, KnapsackSettings settings);
    Task<CryptoResult> DecryptAsync(byte[] cipher, KnapsackSettings settings);
    List<BigInteger> GenerateSuperincreasingSequence(int length);
}