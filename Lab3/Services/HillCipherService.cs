using System;
using Lab3.Models;

namespace Lab3.Services;

public class HillCipherService : IHillCipherService
{
    private readonly ILogService _log;
    private readonly Random _random = new();

    public HillCipherService(ILogService log) => _log = log;

    public byte[] Encrypt(byte[] data, HillCipherSettings settings)
    {
        _log.LogDebug($"[Hill] Encrypt: input={data.Length} bytes, matrix={settings.KeyMatrix?.GetLength(0)}x{settings.KeyMatrix?.GetLength(1)}, mod={settings.Modulo}");

        if (!settings.Validate())
        {
            _log.LogError("[Hill] Invalid key matrix");
            throw new ArgumentException("Неверный ключ Хилла");
        }

        int n = settings.KeyMatrix!.GetLength(0);
        int modulo = settings.Modulo;

        int paddedLength = ((data.Length + n - 1) / n) * n;
        var padded = new byte[paddedLength];
        Array.Copy(data, padded, data.Length);
        _log.LogDebug($"[Hill] Padded: {data.Length} → {paddedLength} bytes");

        var result = new byte[paddedLength];

        for (int i = 0; i < paddedLength; i += n)
        {
            for (int row = 0; row < n; row++)
            {
                int sum = 0;
                for (int col = 0; col < n; col++)
                {
                    sum += settings.KeyMatrix[row, col] * padded[i + col];
                }
                result[i + row] = (byte)((sum % modulo + modulo) % modulo);
            }
        }

        _log.LogDebug($"[Hill] Encrypt complete: {result.Length} bytes");
        return result;
    }

    public byte[] Decrypt(byte[] data, HillCipherSettings settings)
    {
        _log.LogDebug($"[Hill] Decrypt: input={data.Length} bytes, matrix={settings.KeyMatrix?.GetLength(0)}x{settings.KeyMatrix?.GetLength(1)}");

        if (!settings.Validate())
        {
            _log.LogError("[Hill] Invalid key matrix");
            throw new ArgumentException("Неверный ключ Хилла");
        }

        int n = settings.KeyMatrix!.GetLength(0);
        int modulo = settings.Modulo;

        var invMatrix = GetInverseMatrix(settings.KeyMatrix, modulo);
        if (invMatrix == null)
        {
            _log.LogError("[Hill] Matrix not invertible");
            throw new InvalidOperationException("Матрица необратима");
        }
        _log.LogDebug("[Hill] Inverse matrix computed");

        var result = new byte[data.Length];

        for (int i = 0; i < data.Length; i += n)
        {
            for (int row = 0; row < n; row++)
            {
                int sum = 0;
                for (int col = 0; col < n; col++)
                {
                    sum += invMatrix[row, col] * data[i + col];
                }
                result[i + row] = (byte)((sum % modulo + modulo) % modulo);
            }
        }

        _log.LogDebug($"[Hill] Decrypt complete: {result.Length} bytes");
        return result;
    }

    public int[,] GenerateRandomKey(int size, int modulo)
    {
        _log.LogDebug($"[Hill] GenerateKey: size={size}, mod={modulo}");

        int attempts = 0;
        int[,] matrix;
        do
        {
            attempts++;
            matrix = new int[size, size];
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    matrix[i, j] = _random.Next(modulo);
        } while (!IsMatrixInvertible(matrix, modulo) && attempts < 1000);

        if (attempts >= 1000)
        {
            _log.LogError("[Hill] Failed to generate invertible matrix after 1000 attempts");
            throw new InvalidOperationException("Не удалось сгенерировать обратимую матрицу");
        }

        _log.LogDebug($"[Hill] Key generated after {attempts} attempts");
        return matrix;
    }

    public bool IsMatrixInvertible(int[,] matrix, int modulo)
    {
        int det = GetDeterminant(matrix) % modulo;
        if (det < 0) det += modulo;
        bool invertible = GCD(det, modulo) == 1;
        _log.LogDebug($"[Hill] IsInvertible: det={det}, gcd={GCD(det, modulo)}, result={invertible}");
        return invertible;
    }

    private int GetDeterminant(int[,] matrix)
    {
        int n = matrix.GetLength(0);
        if (n == 1) return matrix[0, 0];
        if (n == 2) return matrix[0, 0] * matrix[1, 1] - matrix[0, 1] * matrix[1, 0];
        if (n == 3)
        {
            return matrix[0, 0] * (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1])
                 - matrix[0, 1] * (matrix[1, 0] * matrix[2, 2] - matrix[1, 2] * matrix[2, 0])
                 + matrix[0, 2] * (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]);
        }
        return 1;
    }

    private int GCD(int a, int b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        while (b != 0) { var t = b; b = a % b; a = t; }
        return a;
    }

    private int ModularInverse(int a, int m)
    {
        int m0 = m, x0 = 0, x1 = 1;
        if (m == 1) return 0;
        a = ((a % m) + m) % m;

        while (a > 1)
        {
            if (m == 0) return -1;
            int q = a / m;
            int t = m; m = a % m; a = t;
            t = x0; x0 = x1 - q * x0; x1 = t;
        }
        return x1 < 0 ? x1 + m0 : x1;
    }

    private int[,] GetInverseMatrix(int[,] matrix, int modulo)
    {
        int n = matrix.GetLength(0);
        int det = GetDeterminant(matrix) % modulo;
        if (det < 0) det += modulo;

        _log.LogDebug($"[Hill] GetInverse: det={det}, mod={modulo}");

        int detInv = ModularInverse(det, modulo);
        if (detInv == -1)
        {
            _log.LogError("[Hill] No modular inverse for determinant");
            return null!;
        }

        if (n == 2)
        {
            var inv = new int[,]
            {
                { (matrix[1,1] * detInv) % modulo, ((-matrix[0,1] % modulo + modulo) * detInv) % modulo },
                { ((-matrix[1,0] % modulo + modulo) * detInv) % modulo, (matrix[0,0] * detInv) % modulo }
            };
            _log.LogDebug("[Hill] 2x2 inverse computed");
            return inv;
        }

        if (n == 3)
        {
            var cofactor = new int[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int m00 = matrix[(i + 1) % 3, (j + 1) % 3];
                    int m01 = matrix[(i + 1) % 3, (j + 2) % 3];
                    int m10 = matrix[(i + 2) % 3, (j + 1) % 3];
                    int m11 = matrix[(i + 2) % 3, (j + 2) % 3];
                    int minor = m00 * m11 - m01 * m10;

                    int sign = ((i + j) % 2 == 0) ? 1 : -1;
                    cofactor[i, j] = (sign * minor) % modulo;
                    if (cofactor[i, j] < 0) cofactor[i, j] += modulo;
                }
            }

            var adjugate = new int[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    adjugate[i, j] = (cofactor[j, i] * detInv) % modulo;

            _log.LogDebug("[Hill] 3x3 inverse computed");
            return adjugate;
        }

        _log.LogError($"[Hill] Inverse not implemented for {n}x{n}");
        return null!;
    }
}