using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Ai.Training;

public enum TrainingSampleStatus { New, Approved, Rejected }

/// <summary>KB-Indexierungszustand eines TrainingSamples.</summary>
public enum KbIndexState
{
    /// <summary>Noch nicht durch die KB-Pipeline gelaufen (alte Daten / neu erzeugt).</summary>
    None = 0,
    /// <summary>Indexierung angefordert, aber noch nicht abgeschlossen.</summary>
    Pending,
    /// <summary>Erfolgreich in knowledge_base.db indexiert.</summary>
    Indexed,
    /// <summary>Indexierung fehlgeschlagen (Ollama offline, Embedding-Fehler etc.).</summary>
    Error,
    /// <summary>Vom Dedup-Service abgedeckt — kein eigener KB-Eintrag, gilt aber als abgeschlossen.</summary>
    Deduplicated
}

/// <summary>String-Konstanten fuer TrainingSample.MatchLevel (vermeidet Magic Strings).</summary>
public static class MatchLevelNames
{
    public const string ExactMatch = "ExactMatch";
    public const string PartialMatch = "PartialMatch";
    public const string Mismatch = "Mismatch";
    public const string NoFindings = "NoFindings";
    public const string ReviewApproved = "ReviewApproved";
    public const string ReviewCorrected = "ReviewCorrected";
    public const string TeacherAnnotation = "TeacherAnnotation";
}

/// <summary>String-Konstanten fuer TrainingSample.SourceType (vermeidet Magic Strings).</summary>
public static class SourceTypeNames
{
    public const string PdfPhoto = "PdfPhoto";
    public const string VideoTimestamp = "VideoTimestamp";
    public const string VideoLinear = "VideoLinear";
    public const string BatchImport = "BatchImport";
    public const string TeacherAnnotation = "TeacherAnnotation";
    /// <summary>Slice 1: Operateur klickt Code im Trainingsmodus, zieht Box, SAM-Maske bestaetigt.</summary>
    public const string OperateurAnnotation = "OperateurAnnotation";
}

/// <summary>
/// Phase 5.3 vorbereitend: TrainingSample als pure Domain-Klasse (POCO).
/// Vorher in <c>UI.Ai.Training</c> mit ObservableObject-Erbung — alle Properties
/// waren aber Auto-Props ohne <c>[ObservableProperty]</c>, also gab es nie
/// Property-Notification. Der Wechsel zu POCO ist daher zerstoerungsfrei.
/// </summary>
public sealed class TrainingSample
{
    public string SampleId { get; set; } = string.Empty;
    public string CaseId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Beschreibung { get; set; } = string.Empty;
    public double MeterStart { get; set; }
    public double MeterEnd { get; set; }
    public bool IsStreckenschaden { get; set; }
    public double TimeSeconds { get; set; }
    public double? DetectedMeter { get; set; }
    public string MeterSource { get; set; } = string.Empty;
    public string FramePath { get; set; } = string.Empty;
    public TrainingSampleStatus Status { get; set; } = TrainingSampleStatus.New;
    public DateTime? ExportedUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public double? TruthMeterCenter { get; set; }
    public double? OdsDeltaMeters { get; set; }
    public bool HasOsdMismatch { get; set; }
    public string Signature { get; set; } = string.Empty;
    public int FrameIndex { get; set; }

    /// <summary>Vergleichsergebnis (siehe MatchLevelNames-Konstanten).</summary>
    public string? MatchLevel { get; set; }

    /// <summary>Von der KI erkannter Code (aus ComparisonResult.BestMatchCode).</summary>
    public string? KiCode { get; set; }

    /// <summary>Herkunft des Samples (siehe SourceTypeNames-Konstanten).</summary>
    public string? SourceType { get; set; }

    /// <summary>Aufnahmetechnik-Bewertung: "A", "B", "C" (von TechniqueAssessmentService)</summary>
    public string? TechniqueGrade { get; set; }

    /// <summary>Zusaetzliche Fotos als Lernmaterial (Foto 2 etc. vom Live-Bild).</summary>
    public List<string>? AdditionalFramePaths { get; set; }

    /// <summary>KB-Indexierungszustand (None → Pending → Indexed/Error).</summary>
    public KbIndexState KbIndexState { get; set; } = KbIndexState.None;

    // --- Video-Selbsttraining: Kontextfelder fuer KB-Anreicherung ---

    /// <summary>Rohrmaterial der Haltung (z.B. "Beton", "PVC"). Fuer KB-Embedding-Kontext.</summary>
    public string? Rohrmaterial { get; set; }

    /// <summary>Nennweite in mm (z.B. 300). Fuer KB-Embedding-Kontext.</summary>
    public int? NennweiteMm { get; set; }

    /// <summary>True wenn dieses Sample eine menschliche Korrektur einer KI-Fehlklassifikation ist.</summary>
    public bool IsKorrigiert { get; set; }

    /// <summary>QualityGate-Ampel zum Sample-Zeitpunkt (Green/Yellow/Red).</summary>
    public string? QualityGateLevel { get; set; }

    // ── SAM-Maske (Slice 1, Operateur-Annotation) ────────────────────────
    /// <summary>Run-Length-Encoded Maske vom Sidecar. Format-Tag in <see cref="SamMaskEncoding"/>.</summary>
    public string? SamMaskRle { get; set; }
    /// <summary>Format-Tag fuer Migrations-Faehigkeit (z.B. "sidecar-sam-rle-v1").</summary>
    public string? SamMaskEncoding { get; set; }
    /// <summary>Pixelbreite der Maske (= Frame-Breite zum Annotations-Zeitpunkt).</summary>
    public int? MaskWidth { get; set; }
    /// <summary>Pixelhoehe der Maske.</summary>
    public int? MaskHeight { get; set; }
    /// <summary>Anzahl Pixel innerhalb der Maske (fuer Filter / QualityGate).</summary>
    public int? MaskAreaPixels { get; set; }
    /// <summary>SAM-Confidence der ausgewaehlten Maske.</summary>
    public double? SamConfidence { get; set; }

    /// <summary>Hat eine vollstaendige SAM-Maske (RLE + Dimensionen).</summary>
    public bool HasMask =>
        !string.IsNullOrEmpty(SamMaskRle)
        && MaskWidth.HasValue && MaskWidth.Value > 0
        && MaskHeight.HasValue && MaskHeight.Value > 0;

    /// <summary>
    /// Sekunden-Offset zwischen Anker-Meterstand (PDF) und tatsaechlich annotiertem Frame.
    /// Negativ = Operateur ging zurueck. Null = an Anker codiert.
    /// </summary>
    public double? FrameDeltaSeconds { get; set; }

    // BoundingBox (normiert 0-1, YOLO-Format: center + size)
    /// <summary>BBox X-Center (normiert 0-1). Null = keine BBox vorhanden.</summary>
    public double? BboxXCenter { get; set; }
    /// <summary>BBox Y-Center (normiert 0-1).</summary>
    public double? BboxYCenter { get; set; }
    /// <summary>BBox Breite (normiert 0-1).</summary>
    public double? BboxWidth { get; set; }
    /// <summary>BBox Hoehe (normiert 0-1).</summary>
    public double? BboxHeight { get; set; }

    /// <summary>Hat eine echte BoundingBox (nicht Fallback)?</summary>
    public bool HasBbox => BboxXCenter.HasValue && BboxWidth.HasValue;

    /// <summary>
    /// Zentrale Signatur-Berechnung fuer Dedup.
    /// CaseId ist Teil der Signatur, damit gleiche Codes in verschiedenen Haltungen nicht kollidieren.
    /// Uhrlage ist Teil der Signatur, damit zwei Schaeden am gleichen Meter mit
    /// unterschiedlicher Uhrlage (z.B. 3 Uhr vs 9 Uhr) nicht als Duplikat verworfen werden.
    /// </summary>
    public static string BuildCanonicalSignature(string caseId, string code, double meterCenter, double meterEnd, string? clock = null)
    {
        // Leere Signaturen verhindern — ohne CaseId/Code ist Dedup unmoeglich
        if (string.IsNullOrWhiteSpace(caseId) || string.IsNullOrWhiteSpace(code))
            return "";

        var rc = Math.Round(meterCenter, 1);
        var re = Math.Round(meterEnd, 1);
        // Uhrlage normalisieren: nur die Stunde (1-12), null → leer
        var clockNorm = NormalizeClockForSignature(clock);
        return string.IsNullOrEmpty(clockNorm)
            ? $"{caseId}|{code}|{rc:F1}|{re:F1}"
            : $"{caseId}|{code}|{rc:F1}|{re:F1}|{clockNorm}";
    }

    /// <summary>Extrahiert die Stunde aus einer Uhrlage-Angabe fuer die Signatur.</summary>
    private static string? NormalizeClockForSignature(string? clock)
    {
        if (string.IsNullOrWhiteSpace(clock)) return null;
        // "3 Uhr" → "3", "12:00" → "12", "9" → "9"
        var cleaned = clock.Replace("Uhr", "", StringComparison.OrdinalIgnoreCase)
                           .Replace(":00", "", StringComparison.Ordinal)
                           .Trim();
        return int.TryParse(cleaned, out var h) && h >= 1 && h <= 12 ? h.ToString() : null;
    }
}
