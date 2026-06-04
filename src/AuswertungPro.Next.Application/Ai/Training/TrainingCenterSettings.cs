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

    /// <summary>
    /// Wenn true, darf Auto-Gold nur entstehen, wenn die KB den KI-Code aktiv bestaetigt.
    /// KbNoSignal bleibt Review. Default true = streng fuer unbeaufsichtigte Batch-Laeufe.
    /// </summary>
    public bool RequireKbAgreementForAutoGold { get; set; } = true;

    /// <summary>
    /// Mindestscore fuer Auto-Gold. Heute erreicht nur ExactMatch 1.0; der Wert bleibt als
    /// Reserve fuer spaetere Lockerungen explizit konfigurierbar.
    /// </summary>
    public double AutoAcceptConfidenceThreshold { get; set; } = 1.0;

    /// <summary>
    /// Wenn true, werden per linearer Meter-zu-Zeit-Schaetzung erzeugte VideoFrames nie Auto-Gold.
    /// Sie bleiben Review-Kandidaten, weil ihre Position nicht belastbar genug ist.
    /// </summary>
    public bool RequireReliableFramePositionForAutoGold { get; set; } = true;
}
