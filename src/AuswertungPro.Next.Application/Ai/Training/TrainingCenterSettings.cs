namespace AuswertungPro.Next.Application.Ai.Training;

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
    public int GpuConcurrency { get; set; } = 1;

    /// <summary>
    /// S2b-Sicherheitsschalter: Wenn true, wird im Self-Training NICHTS automatisch als Gold/KB
    /// uebernommen — auch ein sauberer 4-Achsen-ExactMatch (S2) bleibt nur Kandidat und geht in die
    /// ReviewQueue (Grund: HumanReviewRequired). Default true = sicher fuer unbeaufsichtigte Nachtlaeufe.
    /// </summary>
    public bool RequireHumanReview { get; set; } = true;
}
