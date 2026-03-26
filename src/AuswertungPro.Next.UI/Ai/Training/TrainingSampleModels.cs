using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Ai.Training
{
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
        Error
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
    }

    /// <summary>String-Konstanten fuer TrainingSample.SourceType (vermeidet Magic Strings).</summary>
    public static class SourceTypeNames
    {
        public const string PdfPhoto = "PdfPhoto";
        public const string VideoTimestamp = "VideoTimestamp";
        public const string VideoLinear = "VideoLinear";
        public const string BatchImport = "BatchImport";
        public const string TeacherAnnotation = "TeacherAnnotation";
    }

    public sealed partial class TrainingSample : ObservableObject
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
        /// </summary>
        public static string BuildCanonicalSignature(string caseId, string code, double meterCenter, double meterEnd)
        {
            var rc = Math.Round(meterCenter, 1);
            var re = Math.Round(meterEnd, 1);
            return $"{caseId}|{code}|{rc:F1}|{re:F1}";
        }
    }
}
