namespace Lab3.Models;

public class StegoSettings
{
    public int BitsPerChannel { get; set; } = 1; // 1-5 бит
    public string? InputText { get; set; }
    public string? InputFilePath { get; set; }
    public string? ImagePath { get; set; }
    public string? OutputImagePath { get; set; }
    public string? OutputTextPath { get; set; }

    public bool Validate() =>
        BitsPerChannel is >= 1 and <= 5 &&
        !string.IsNullOrWhiteSpace(InputText) &&
        !string.IsNullOrWhiteSpace(ImagePath);
}