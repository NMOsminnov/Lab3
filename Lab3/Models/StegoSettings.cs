// Lab3/Models/StegoSettings.cs
public class StegoSettings
{
    // ✅ Существующие:
    public int BitsPerChannel { get; set; } = 1;
    public string? InputText { get; set; }
    public string? InputFilePath { get; set; }
    public string? ImagePath { get; set; }
    public string? OutputImagePath { get; set; }
    public string? OutputTextPath { get; set; }

    // ➕ Новые для Лабы 4-5:
    public EmbeddingMode EmbeddingMode { get; set; } = EmbeddingMode.RowMajor;
    public bool UseHillCipher { get; set; } = true;
    public int HillKeySize { get; set; } = 2; // 2 или 3
    public int[,]? HillKeyMatrix { get; set; } // Если null — сгенерировать случайно
    public bool UseZipCompression { get; set; } = true;
    public bool UseHammingCode { get; set; } = false;
    public int DistortionErrorCount { get; set; } = 0;

    public bool Validate() =>
        BitsPerChannel is >= 1 and <= 5 &&
        !string.IsNullOrWhiteSpace(InputText) &&
        !string.IsNullOrWhiteSpace(ImagePath);
}

// ➕ Новый тип для режима внедрения
public enum EmbeddingMode { RowMajor, ColumnMajor }