using System;
using System.Globalization;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

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
    /// Adaptive Defaults je nach erkanntem VRAM:
    /// >=24 GB: 4, >=12 GB: 3, sonst 2.
    /// </summary>
    public int GpuConcurrency { get; set; } = ResolveDefaultGpuConcurrency();

    /// <summary>
    /// CPU-Parallelitaet fuer die Vorab-Extraktion von PDF-Fotos.
    /// Hoeherer Wert nutzt mehr Kerne und RAM, beschleunigt aber grosse Batches.
    /// </summary>
    public int CpuPreExtractParallelism { get; set; } = ResolveDefaultCpuPreExtractParallelism();

    /// <summary>
    /// Anzahl gleichzeitig verarbeiteter Faelle im Batch-Selbsttraining.
    /// Jeder Fall hat eigene GPU-Requests, daher nur moderat erhoehen.
    /// </summary>
    public int CaseParallelism { get; set; } = ResolveDefaultCaseParallelism();

    /// <summary>
    /// Wenn true: PartialMatch-Samples mit passendem Code und Confidence >= PartialAutoApproveMinScore
    /// werden automatisch approved und in die KnowledgeBase indexiert — ohne manuellen Review.
    /// QualityGate und Dedup-Service filtern als zweite/dritte Sicherheitsebene.
    /// Erhoeht die Lernmenge bei PDF-Fotos, wenn nur Meter/Clock leicht abweichen.
    /// </summary>
    public bool AutoApproveHighConfidenceCodeHits { get; set; } = true;

    /// <summary>
    /// Mindestscore fuer Auto-Approve bei Partial-Match (0-1).
    /// Empfohlen 0.55-0.70.
    /// </summary>
    public double PartialAutoApproveMinScore { get; set; } = 0.60;

    /// <summary>
    /// Guided Verification fuer Non-Exact-Faelle aktivieren.
    /// Nutzt einen zweiten, protokollgefuehrten KI-Check als Rettungsnetz.
    /// </summary>
    public bool EnableGuidedVerification { get; set; } = true;

    /// <summary>
    /// Maximale Anzahl Guided-Verification-Aufrufe pro Haltung.
    /// Begrenzung fuer Laufzeitkontrolle.
    /// </summary>
    public int GuidedVerificationBudgetPerCase { get; set; } = 12;

    private static int ResolveDefaultGpuConcurrency()
    {
        var explicitOverride = ReadPositiveIntEnv("SEWERSTUDIO_GPU_CONCURRENCY");
        if (explicitOverride is { } ov)
            return Math.Clamp(ov, 1, 12);

        var detected = 2;
        try
        {
            var profile = GpuModelSelector.DetectAndSelect();
            var vramMb = profile?.VramTotalMb ?? 0;
            // VRAM ist gross genug, aber Ollama serialisiert Requests intern.
            // Mehr als 3 parallele Qwen-Requests fuehren zu Timeouts (>60s Wartezeit).
            if (vramMb >= 30_000) detected = 3;  // RTX 5090 32GB — Ollama-Limit beachten
            else if (vramMb >= 24_000) detected = 3;
            else if (vramMb >= 12_000) detected = 2;
        }
        catch
        {
            // Fallback auf konservativen Default.
        }

        // Wenn Ollama bereits mit mehr Slots gestartet wurde, daran ausrichten.
        var ollamaParallel = ReadPositiveIntEnv("OLLAMA_NUM_PARALLEL");
        if (ollamaParallel is { } slots)
            detected = Math.Max(detected, slots);

        return Math.Clamp(detected, 1, 12);
    }

    private static int ResolveDefaultCpuPreExtractParallelism()
    {
        var explicitOverride = ReadPositiveIntEnv("SEWERSTUDIO_SELFTRAIN_PREEXTRACT_PARALLELISM");
        if (explicitOverride is { } ov)
            return Math.Clamp(ov, 1, 48);

        return Math.Clamp(Environment.ProcessorCount - 2, 4, 24);
    }

    private static int ResolveDefaultCaseParallelism()
    {
        var explicitOverride = ReadPositiveIntEnv("SEWERSTUDIO_SELFTRAIN_CASE_PARALLELISM");
        if (explicitOverride is { } ov)
            return Math.Clamp(ov, 1, 8);

        try
        {
            var profile = GpuModelSelector.DetectAndSelect();
            var vramMb = profile?.VramTotalMb ?? 0;
            if (vramMb >= 24_000) return 3;
            if (vramMb >= 12_000) return 2;
        }
        catch
        {
            // Fallback unten.
        }

        return 2;
    }

    private static int? ReadPositiveIntEnv(string variable)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;
    }
}
