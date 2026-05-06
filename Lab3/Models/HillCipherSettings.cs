namespace Lab3.Models;

public class HillCipherSettings
{
    // Матрица ключа (например, 2×2 или 3×3)
    public int[,] KeyMatrix { get; set; }

    // Размер алфавита (обычно 256 для байтов, 26 для букв)
    public int Modulo { get; set; } = 256;

    // Проверка: матрица должна быть квадратной и обратимой
    public bool Validate()
    {
        if (KeyMatrix == null) return false;
        int n = KeyMatrix.GetLength(0);
        if (KeyMatrix.GetLength(1) != n) return false;

        // Простая проверка: определитель не должен быть 0
        return GetDeterminant(KeyMatrix) % Modulo != 0;
    }

    private int GetDeterminant(int[,] matrix)
    {
        int n = matrix.GetLength(0);
        if (n == 2)
            return matrix[0, 0] * matrix[1, 1] - matrix[0, 1] * matrix[1, 0];
        // Для 3×3 и больше можно добавить полный расчёт
        return 1; // Заглушка для примера
    }
}