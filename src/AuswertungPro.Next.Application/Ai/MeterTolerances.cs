namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Zentrale Meter-Toleranzen fuer die gesamte KI-Pipeline (Audit N3).
/// Kontextabhaengig: Benchmark ist strikt, Batch ist tolerant wegen OSD-Rauschen.
/// </summary>
public static class MeterTolerances
{
    /// <summary>Benchmark: strikt, misst echte Praezision (±0.5m).</summary>
    public const double Benchmark = 0.5;

    /// <summary>Einzelhaltungs-Training / DifferenceAnalyzer (±0.5m).</summary>
    public const double SingleTraining = 0.5;

    /// <summary>Deterministische Vergleiche im SelfTrainingComparisonService (±1.0m).</summary>
    public const double SelfTrainingComparison = 1.0;

    /// <summary>Batch-Nachtbetrieb: toleranter wegen OSD-Rauschen in realen Videos (±1.5m).</summary>
    public const double BatchProcessing = 1.5;
}
