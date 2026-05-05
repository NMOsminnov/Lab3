using System.Numerics;

namespace Lab3.Models;

public class RsaKeys
{
    public BigInteger E { get; set; }      // Открытая экспонента
    public BigInteger N { get; set; }      // Модуль
    public BigInteger D { get; set; }      // Закрытая экспонента
    public int PrimeP { get; set; }        // Простое p (для отладки/отчёта)
    public int PrimeQ { get; set; }        // Простое q (для отладки/отчёта)
}