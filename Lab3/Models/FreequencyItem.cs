// Lab3/Models/FrequencyItem.cs
namespace Lab3.Models;

public class FrequencyItem
{
    public string BlockValue { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }

    // ✅ Для отображения: обрезанное значение блока
    public string ShortValue =>
        BlockValue.Length > 15 ? BlockValue.Substring(0, 12) + "..." : BlockValue;

    // ✅ Для таблицы: отформатированный процент
    public string PercentageFormatted => $"{Percentage:F2}%";

    // ✅ ДЛЯ ГРАФИКА: высота столбца в пикселях (макс. 200px)
    // Вычисляется один раз при анализе, передаётся извне
    public double BarHeight { get; set; }

    // ✅ Подпись для оси X (короткая)
    public string XLabel => Count > 1 ? $"×{Count}" : "";
}