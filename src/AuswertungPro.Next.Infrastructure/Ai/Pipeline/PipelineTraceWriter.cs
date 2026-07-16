using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Kompatibilitaetsfassade fuer den Frame-Ablauf eines KI-Laufs. Die Dateiarbeit
/// liegt im Instanzdienst und aendert kein Pipeline-Verhalten.
/// </summary>
public static class PipelineTraceWriter
{
    private static readonly IPipelineTraceWriter Default = new PipelineTraceFileWriter();

    public static IPipelineTraceWriter Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IPipelineTraceWriter writer) =>
        throw new NotSupportedException(
            "Der globale Pipeline-Trace-Schreiber kann nicht mehr ausgetauscht werden. " +
            "IPipelineTraceWriter bitte per Konstruktor uebergeben.");

    public static async Task WriteAsync(PipelineFrameTrace entry)
    {
        var mapped = PipelineTraceEntryMapper.Map(entry);

        await PipelineTraceWriteGuard.WriteAsync(Current, mapped).ConfigureAwait(false);
    }

    /// <summary>
    /// Schreibt die aggregierte Zusammenfassung neben die Trace-Datei, damit
    /// Stufen-Latenzen ohne Log-Auswertung verfuegbar bleiben.
    /// </summary>
    public static async Task WriteSummaryAsync(string runId, TelemetrySummary summary)
        => await PipelineTraceWriteGuard
            .WriteSummaryAsync(Current, runId, summary)
            .ConfigureAwait(false);

    public static string? ResolvePath(string runId)
        => PipelineTraceWriteGuard.ResolvePath(Current, runId);

    public static string? ResolveSummaryPath(string runId)
        => PipelineTraceWriteGuard.ResolveSummaryPath(Current, runId);
}

/// <summary>
/// Ein Trace-Eintrag pro Frame. Mutable, weil er waehrend der Frame-Verarbeitung
/// stufenweise befuellt und am jeweiligen End- oder Abbruchpunkt geschrieben wird.
/// </summary>
public sealed class PipelineFrameTrace
{
    public string RunId { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; }
    public int FrameIndex { get; set; }
    public double TimeSec { get; set; }
    public double Meter { get; set; }

    /// <summary>Verarbeitungspfad, etwa processed, empty_frame oder dino_error.</summary>
    public string Path { get; set; } = "processed";

    public bool YoloBypass { get; set; }
    public bool? YoloRelevant { get; set; }
    public int YoloDetectionCount { get; set; }
    public int DinoBoxCount { get; set; }
    public int SamMaskCount { get; set; }

    /// <summary>Befunde aus SAM-Masken vor der Qwen-Anreicherung.</summary>
    public int FindingsBuilt { get; set; }

    /// <summary>Aus DINO-Beschriftungen abgeleitete VSA-Codes.</summary>
    public int CodesFromLabel { get; set; }

    /// <summary>Vom Klassifikator aufgeloester Code vor dem zeitlichen Voting.</summary>
    public string? ClassifierCode { get; set; }
    public double? ClassifierConfidence { get; set; }

    /// <summary>Begruendung der Klassifikatorentscheidung.</summary>
    public string? ClassifierSource { get; set; }

    /// <summary>Verwendete Modellversion.</summary>
    public string? ClassifierModel { get; set; }

    /// <summary>Gibt an, ob das zeitliche Voting den Code bestaetigt hat.</summary>
    public bool? ClassifierVoteConfirmed { get; set; }
    public bool QwenCalled { get; set; }
    public string? QwenImageQuality { get; set; }
    public int QwenRawFindingCount { get; set; }

    /// <summary>Befunde mit VSA-Code nach der Qwen-Anreicherung.</summary>
    public int CodesAfterQwen { get; set; }

    /// <summary>Befunde am Frame-Ende.</summary>
    public int FindingsEndOfFrame { get; set; }

    /// <summary>Aktive Befunde im Dedup-Puffer nach diesem Frame.</summary>
    public int ActiveCount { get; set; }

    /// <summary>Bisher abgeschlossene Erkennungen des Laufs.</summary>
    public int DetectionsTotal { get; set; }

    /// <summary>Grund fuer verlorene oder verworfene Befunde.</summary>
    public string? DropReason { get; set; }

    /// <summary>Ein Modell- oder Inferenzfehler macht den Frame pruefbeduerftig.</summary>
    public bool Degraded { get; set; }

    /// <summary>Technischer Grund des eingeschraenkten Zustands.</summary>
    public string? DegradedReason { get; set; }
}
