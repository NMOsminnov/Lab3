using System.Collections.Generic;

namespace Lab3.Models;

public class SteganalysisReport
{
    public int BitsPerChannel { get; set; }
    public double Threshold { get; set; }
    public List<SteganalysisPoint> Points { get; set; } = new();
}

public class SteganalysisPoint
{
    public double FillPercentage { get; set; }
    public double DetectionRate { get; set; } // 0..100%
    public int TotalTests { get; set; }
    public int DetectedCount { get; set; }
}