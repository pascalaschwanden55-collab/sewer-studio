namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class TrainingCenterSettings
{
    /// <summary>
    /// Schwellwert fuer OSD-Mismatch (Abweichung zwischen Protokoll-Meter und OSD-Meter).
    /// Bei Kanalvideos ist die lineare Zeitschaetzung oft 10-20m ungenau,
    /// daher muss der Threshold grosszuegig sein um nicht 80%+ der Samples auszuschliessen.
    /// </summary>
    public double OsdMismatchThresholdMeters { get; set; } = 20.0;
    public int RangeSampleCount { get; set; } = 5;
    public double MinRangeLengthForSampling { get; set; } = 0.50;
    public int TimelineSampleCount { get; set; } = 30;
    public string? FramesOutputFolder { get; set; } = null; // null = default AppData folder
    /// <summary>
    /// Anzahl paralleler GPU-Requests (Ollama).
    /// RTX 5090 (32GB): 4 empfohlen — erfordert OLLAMA_NUM_PARALLEL=4.
    /// Kleinere GPUs (8-16GB): 1-2.
    /// </summary>
    /// <summary>
    /// Anzahl paralleler GPU-Requests (Ollama).
    /// Muss mit OLLAMA_NUM_PARALLEL uebereinstimmen.
    /// RTX 5090 (32GB): 2 empfohlen (mit ctx=32768).
    /// </summary>
    public int GpuConcurrency { get; set; } = 2;
}
