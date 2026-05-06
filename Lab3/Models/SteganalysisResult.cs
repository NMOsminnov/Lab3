namespace Lab3.Models;

public class SteganalysisResult
{
    public string ImageName { get; set; } = string.Empty;
    public int BitsPerChannel { get; set; }
    public double FillPercentage { get; set; }
    public double ChiSquare { get; set; }
    public double PValue { get; set; }
    public bool IsDetected { get; set; }
    public string Notes { get; set; } = string.Empty;
}