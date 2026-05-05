using System.Collections.Generic;
using System.Numerics;

namespace Lab3.Models;

// Lab3/Models/KnapsackSettings.cs
public class KnapsackSettings
{
    // 🔥 Изменили ограничение с 150 на 200
    public int SequenceLength { get; set; } = 200; // теперь по умолчанию 200

    public List<BigInteger>? PublicKey { get; set; }
    public BigInteger Multiplier { get; set; }
    public BigInteger Modulus { get; set; }
    public string? InputText { get; set; }
    public string? InputFilePath { get; set; }
    public List<BigInteger>? SuperincreasingSequence { get; set; }

    // 🔥 Обновлённая валидация
    public bool Validate() =>
        SequenceLength is > 0 and <= 200 &&  // <-- 200 вместо 150
        Multiplier > 0 && Modulus > 0 &&
        !string.IsNullOrWhiteSpace(InputText);
}