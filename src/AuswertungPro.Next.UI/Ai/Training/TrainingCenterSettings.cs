namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class TrainingCenterSettings
{
    public double OsdMismatchThresholdMeters { get; set; } = 0.50;
    public int RangeSampleCount { get; set; } = 5;
    public double MinRangeLengthForSampling { get; set; } = 0.50;
    public int TimelineSampleCount { get; set; } = 60;
    public string? FramesOutputFolder { get; set; } = null; // null = default AppData folder
}
