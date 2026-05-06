using Lab3.Models;
using System.Threading.Tasks;

namespace Lab3.Services;

public interface IHillCipherService
{
    byte[] Encrypt(byte[] data, HillCipherSettings settings);
    byte[] Decrypt(byte[] data, HillCipherSettings settings);

    // Вспомогательные методы для генерации ключа
    int[,] GenerateRandomKey(int size, int modulo);
    bool IsMatrixInvertible(int[,] matrix, int modulo);
}