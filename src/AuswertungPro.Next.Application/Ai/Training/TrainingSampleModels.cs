using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai.Training;

public enum TrainingSampleStatus { New, Approved, Rejected, Removed, Draft }

/// <summary>KB-Indexierungszustand eines TrainingSamples.</summary>
public enum KbIndexState
{
    /// <summary>Noch nicht durch die KB-Pipeline gelaufen (alte Daten oder neu erzeugt).</summary>
    None = 0,

    /// <summary>Indexierung angefordert, aber noch nicht abgeschlossen.</summary>
    Pending,

    /// <summary>Erfolgreich in knowledge_base.db indexiert.</summary>
    Indexed,

    /// <summary>Indexierung fehlgeschlagen (echter Schreib-/Embedding-Fehler – ein Wiederholversuch ist sinnvoll).</summary>
    Error,

    /// <summary>
    /// Bewusst und dauerhaft NICHT indexiert: Eval-kontaminiert oder nicht index-wuerdig
    /// (zu kurze Beschreibung, unbekannter Code, nicht trainingsfaehig, fachlich implausibel).
    /// Anders als <see cref="Error"/> KEIN Fehler – ein Nachhol-Lauf darf solche Samples
    /// NICHT erneut versuchen (es waere immer dasselbe Ergebnis).
    /// </summary>
    Skipped
}

/// <summary>String-Konstanten fuer TrainingSample.MatchLevel.</summary>
public static class MatchLevelNames
{
    public const string ExactMatch = "ExactMatch";
    public const string PartialMatch = "PartialMatch";
    public const string Mismatch = "Mismatch";
    public const string NoFindings = "NoFindings";
    public const string ReviewApproved = "ReviewApproved";
    public const string ReviewCorrected = "ReviewCorrected";
}

/// <summary>String-Konstanten fuer TrainingSample.SourceType.</summary>
public static class SourceTypeNames
{
    public const string PdfPhoto = "PdfPhoto";
    public const string VideoTimestamp = "VideoTimestamp";
    public const string VideoLinear = "VideoLinear";
    public const string BatchImport = "BatchImport";
    public const string TeacherAnnotation = "TeacherAnnotation";
    public const string ManualCoding = "ManualCoding";
    public const string ImportedProtocol = "ImportedProtocol";
}

/// <summary>
/// Woher die menschliche Entscheidung stammt: mit oder ohne sichtbaren
/// Modellvorschlag. <see cref="Unknown"/> ist der Standard und gilt fuer den
/// gesamten Altbestand — unbekannt zaehlt bewusst nicht als unabhaengig.
/// </summary>
public enum TrainingSampleSuggestionOrigin
{
    Unknown = 0,
    Independent = 1,
    SuggestionShown = 2
}

/// <summary>
/// Haelt fest, ob dem Menschen beim Codieren ein Modellvorschlag sichtbar war
/// und von welchem Modell er stammte. Ein Copilot verzerrt die Daten, die er
/// selbst erzeugt; ohne diese Angabe laesst sich spaeter nicht mehr trennen,
/// was der Mensch selbst gefunden und was er nur bestaetigt hat.
/// </summary>
public sealed class TrainingSampleSuggestionProvenance
{
    public TrainingSampleSuggestionOrigin Origin { get; set; }

    /// <summary>ID des gepinnten Kandidaten, der den Vorschlag erzeugt hat.</summary>
    public string? ModelId { get; set; }

    /// <summary>SHA-256 des Gewichts — bindet den Vorschlag an genau ein Artefakt.</summary>
    public string? ModelSha256 { get; set; }

    public string? SuggestedCode { get; set; }

    public double? SuggestedConfidence { get; set; }
}

public sealed class TrainingSample
{
    public string SampleId { get; set; } = string.Empty;
    public string CaseId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Beschreibung { get; set; } = string.Empty;
    public double MeterStart { get; set; }
    public double MeterEnd { get; set; }

    /// <summary>
    /// Wahr, wenn zur Quelle gar kein Meterstand vorlag. <see cref="MeterStart"/>
    /// und <see cref="MeterEnd"/> stehen dann auf 0 und sind KEIN Messwert.
    ///
    /// Bewusst ein Kennzeichen statt eines nullbaren Typs — dasselbe Muster wie
    /// BendSuggestion.MeterIsEstimated und das Feld VSA_Geschaetzt. Das haelt den
    /// Altbestand gueltig: Ein fehlendes Feld liest sich als false und bedeutet
    /// damit "Meter bekannt", so wie es bisher immer gemeint war
    /// (Codeaudit 2026-08-17).
    /// </summary>
    public bool MeterIsUnknown { get; set; }

    public bool IsStreckenschaden { get; set; }
    public double TimeSeconds { get; set; }
    public double? DetectedMeter { get; set; }
    public string MeterSource { get; set; } = string.Empty;
    public string FramePath { get; set; } = string.Empty;
    /// <summary>Markiertes Beweisbild fuer Mensch/Protokoll. Kein Lernmaterial.</summary>
    public string? EvidenceFramePath { get; set; }
    public TrainingSampleStatus Status { get; set; } = TrainingSampleStatus.New;
    public DateTime? ExportedUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public double? TruthMeterCenter { get; set; }
    public double? OdsDeltaMeters { get; set; }
    public bool HasOsdMismatch { get; set; }
    public string Signature { get; set; } = string.Empty;
    public int FrameIndex { get; set; }

    /// <summary>Vergleichsergebnis, siehe MatchLevelNames.</summary>
    public string? MatchLevel { get; set; }

    /// <summary>Von der KI erkannter Code.</summary>
    public string? KiCode { get; set; }

    /// <summary>KB-Abgleich-Signal (Weg 1): "KbAgreement" / "KbDisagreement" / "KbNoSignal".</summary>
    public string? KbCheck { get; set; }

    /// <summary>Herkunft des Samples, siehe SourceTypeNames.</summary>
    public string? SourceType { get; set; }

    /// <summary>
    /// War beim Codieren ein Modellvorschlag sichtbar? Fehlt das Feld, gilt die
    /// Herkunft als unbekannt und das Sample taugt nicht zum Messen.
    /// Siehe <see cref="SuggestionProvenancePolicy"/>.
    /// </summary>
    public TrainingSampleSuggestionProvenance? SuggestionProvenance { get; set; }

    /// <summary>
    /// Urspruenglicher Code einer externen, bereits codierten Referenz.
    /// Der persoenlich bestaetigte finale <see cref="Code"/> darf davon abweichen.
    /// </summary>
    public string? SourceReferenceCode { get; set; }

    /// <summary>
    /// Urspruenglicher Befundtext einer externen Referenz. Er bleibt neben einer
    /// persoenlichen Korrektur erhalten und ist kein automatischer KI-Vorschlag.
    /// </summary>
    public string? SourceReferenceDescription { get; set; }

    /// <summary>Strukturierte VSA-Zusatzdaten aus Import oder Codiermodus.</summary>
    public ProtocolEntryCodeMeta? CodeMeta { get; set; }

    /// <summary>Aufnahmetechnik-Bewertung: A, B oder C.</summary>
    public string? TechniqueGrade { get; set; }

    /// <summary>Zusaetzliche Fotos als Lernmaterial.</summary>
    public List<string>? AdditionalFramePaths { get; set; }

    /// <summary>KB-Indexierungszustand.</summary>
    public KbIndexState KbIndexState { get; set; } = KbIndexState.None;

    /// <summary>Aufnahme-/Inspektionsdatum. Null = nicht trainingsfaehig.</summary>
    public DateTime? InspectionDate { get; set; }

    /// <summary>Harte Trainingsfreigabe nach Datum und Datenherkunft.</summary>
    public bool TrainingEligible { get; set; }

    /// <summary>Grund, warum ein Sample nicht ins Training darf.</summary>
    public string? TrainingEligibilityReason { get; set; }

    // ── Gold-Fund-Metadaten (nullable/default — alte JSON-Samples bleiben gueltig) ──
    /// <summary>true=Mensch bestaetigt (Accept/Edit), false=abgelehnt, null=nie menschlich beurteilt.</summary>
    public bool? HumanConfirmed { get; set; }
    /// <summary>true=Mensch hat den KI-Code korrigiert (Edit+Accept). null=nie beurteilt.</summary>
    public bool? Corrected { get; set; }
    /// <summary>Name des Bestaetigers (Bearbeiter). Null = unbekannt/alt.</summary>
    public string? ConfirmedByUser { get; set; }
    /// <summary>UTC-Zeitpunkt der Bestaetigung. Null = unbekannt/alt.</summary>
    public DateTime? ConfirmedAtUtc { get; set; }
    /// <summary>QualityGate-Ampel zum Bestaetigungszeitpunkt: "Green"/"Yellow"/"Red". Null = unbekannt.</summary>
    public string? QualityGateLevel { get; set; }
    /// <summary>Versioniertes Urteil der zentralen KI-Freigabe, falls vorhanden.</summary>
    public AiDecisionAudit? CentralDecision { get; set; }
    /// <summary>Grund, falls der Snapshot beim Akzeptieren nicht gezogen werden konnte. Null = ok.</summary>
    public string? SnapshotError { get; set; }

    /// <summary>BBox X-Center, normiert 0-1. Null = keine BBox vorhanden.</summary>
    public double? BboxXCenter { get; set; }

    /// <summary>BBox Y-Center, normiert 0-1.</summary>
    public double? BboxYCenter { get; set; }

    /// <summary>BBox Breite, normiert 0-1.</summary>
    public double? BboxWidth { get; set; }

    /// <summary>BBox Hoehe, normiert 0-1.</summary>
    public double? BboxHeight { get; set; }

    /// <summary>Hat eine echte BoundingBox.</summary>
    public bool HasBbox => BboxXCenter.HasValue && BboxYCenter.HasValue && BboxWidth.HasValue && BboxHeight.HasValue;

    /// <summary>SAM-RLE-Maske fuer gepruefte Segmentierungslabels. Null = keine Maske vorhanden.</summary>
    public string? SamMaskRle { get; set; }

    /// <summary>Original-Bildbreite der SAM-Maske in Pixeln.</summary>
    public int? SamMaskImageWidth { get; set; }

    /// <summary>Original-Bildhoehe der SAM-Maske in Pixeln.</summary>
    public int? SamMaskImageHeight { get; set; }

    /// <summary>Maskenflaeche in Pixeln.</summary>
    public int? SamMaskAreaPixels { get; set; }

    /// <summary>SAM-Konfidenz fuer die gespeicherte Maske.</summary>
    public double? SamMaskConfidence { get; set; }

    /// <summary>Label der SAM-Maske.</summary>
    public string? SamMaskLabel { get; set; }

    /// <summary>Hat eine echte SAM-Maske.</summary>
    public bool HasSamMask =>
        !string.IsNullOrWhiteSpace(SamMaskRle)
        && SamMaskImageWidth is > 0
        && SamMaskImageHeight is > 0;

    /// <summary>
    /// Zentrale Signatur-Berechnung fuer Dedup.
    /// CaseId ist Teil der Signatur, damit gleiche Codes in verschiedenen Haltungen nicht kollidieren.
    /// </summary>
    public static string BuildCanonicalSignature(string caseId, string code, double meterCenter, double meterEnd)
        => BuildCanonicalSignature(caseId, code, meterCenter, meterEnd, null, null, null, null);

    /// <summary>
    /// Zentrale Signatur-Berechnung fuer Dedup, optional mit Objekt-Geometrie.
    /// Bei vollstaendiger Box gehoert sie zur Objekt-Identitaet (Mehrfachobjekt):
    /// zwei Befunde mit gleichem Code/Meter, aber verschiedenen Boxen sind verschiedene
    /// Objekte (Format "...|b:x,y,w,h", je 3 Dezimalstellen, InvariantCulture).
    /// Ohne Box bleibt das 4-Teiler-Format — Legacy-Daten bleiben gueltig.
    /// </summary>
    /// <param name="meterIsUnknown">
    /// Wahr, wenn zur Quelle gar kein Meterstand vorlag. Dann steht in der
    /// Signatur ein Fragezeichen statt einer Zahl: Ein Befund ohne bekannten Ort
    /// ist etwas anderes als einer am Rohranfang, und genau diese Verwechslung
    /// hat 809 von 1787 Goldsamples auf Meter 0,00 gesetzt — darunter 25-mal
    /// BCE (Rohrende), das dort per Definition nicht liegen kann
    /// (Codeaudit 2026-08-17).
    ///
    /// Der Standardwert false haelt jede bestehende Signatur unveraendert:
    /// Altdaten ohne dieses Feld gelten weiterhin als "Meter bekannt", sonst
    /// griffe die Dublettensperre nicht mehr und erneutes Akzeptieren legte
    /// Zweitstuecke an.
    /// </param>
    public static string BuildCanonicalSignature(
        string caseId,
        string code,
        double meterCenter,
        double meterEnd,
        double? bboxXCenter,
        double? bboxYCenter,
        double? bboxWidth,
        double? bboxHeight,
        bool meterIsUnknown = false)
    {
        var rc = Math.Round(meterCenter, 1);
        var re = Math.Round(meterEnd, 1);
        var baseSignature = meterIsUnknown
            ? $"{caseId}|{code}|?|?"
            : $"{caseId}|{code}|{rc:F1}|{re:F1}";
        if (bboxXCenter is null || bboxYCenter is null || bboxWidth is null || bboxHeight is null)
            return baseSignature;

        string F3(double value) => Math.Round(value, 3).ToString("F3", CultureInfo.InvariantCulture);
        return $"{baseSignature}|b:{F3(bboxXCenter.Value)},{F3(bboxYCenter.Value)},{F3(bboxWidth.Value)},{F3(bboxHeight.Value)}";
    }
}

public readonly record struct TrainingEligibilityResult(bool IsEligible, string? Reason);

public static class TrainingSampleEligibility
{
    public static readonly DateTime MinimumInspectionDate = new(2022, 1, 1);
    public const string MissingInspectionDateReason = "missing-inspection-date";
    public const string LegacyBeforeCutoffReason = "legacy-before-2022";
    public const string InvalidCatalogCodeReason = "code-not-in-catalog";

    public static TrainingEligibilityResult Evaluate(DateTime? inspectionDate)
    {
        if (inspectionDate is null)
            return new TrainingEligibilityResult(false, MissingInspectionDateReason);

        return inspectionDate.Value.Date >= MinimumInspectionDate
            ? new TrainingEligibilityResult(true, null)
            : new TrainingEligibilityResult(false, LegacyBeforeCutoffReason);
    }

    public static TrainingEligibilityResult Evaluate(TrainingSample sample)
    {
        if (ManualGoldTrainingPolicy.IsManuallyConfirmed(sample))
            return new TrainingEligibilityResult(true, null);

        var result = Evaluate(sample.InspectionDate);
        if (!result.IsEligible)
            return result;

        return sample.TrainingEligible
            ? result
            : new TrainingEligibilityResult(false, sample.TrainingEligibilityReason ?? MissingInspectionDateReason);
    }

    public static TrainingEligibilityResult Evaluate(TrainingSample sample, ICodeCatalogProvider catalog)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(catalog);

        var result = Evaluate(sample);
        if (!result.IsEligible)
            return result;

        if (string.IsNullOrWhiteSpace(sample.Code))
            return new TrainingEligibilityResult(false, InvalidCatalogCodeReason);

        return catalog.TryGet(sample.Code.Trim(), out var def)
               && def.IsSelectable
               && !def.IsObservedExtension
            ? result
            : new TrainingEligibilityResult(false, InvalidCatalogCodeReason);
    }

    public static DateTime? TryParseInspectionDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[]
        {
            "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy",
            "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
            "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd"
        };

        if (DateTime.TryParseExact(
                text,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var exact))
        {
            return exact.Date;
        }

        var dateMatch = Regex.Match(text, @"\b(?<d>\d{1,2})[./-](?<m>\d{1,2})[./-](?<y>\d{2,4})\b");
        if (dateMatch.Success)
        {
            var day = int.Parse(dateMatch.Groups["d"].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(dateMatch.Groups["m"].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(dateMatch.Groups["y"].Value, CultureInfo.InvariantCulture);
            if (year < 100)
                year += year >= 70 ? 1900 : 2000;
            if (TryCreateDate(year, month, day, out var parsed))
                return parsed;
        }

        var isoMatch = Regex.Match(text, @"\b(?<y>\d{4})[-/](?<m>\d{1,2})[-/](?<d>\d{1,2})\b");
        if (isoMatch.Success)
        {
            var year = int.Parse(isoMatch.Groups["y"].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(isoMatch.Groups["m"].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(isoMatch.Groups["d"].Value, CultureInfo.InvariantCulture);
            if (TryCreateDate(year, month, day, out var parsed))
                return parsed;
        }

        // Eingebettetes yyyyMMdd-Datum, z.B. im Dateinamen-Praefix "20251110_9866-9327.pdf".
        // 8 zusammenhaengende Ziffern, die NICHT Teil einer laengeren Ziffernfolge sind (Lookarounds),
        // links-nach-rechts der erste gueltige Kalendertag mit plausiblem Jahr [1990,2099].
        // Laeuft erst nach Exact/Trennzeichen-Formaten und VOR dem reinen Jahres-Fallback.
        foreach (Match compact in Regex.Matches(text, @"(?<!\d)(?<y>\d{4})(?<m>\d{2})(?<d>\d{2})(?!\d)"))
        {
            var cy = int.Parse(compact.Groups["y"].Value, CultureInfo.InvariantCulture);
            var cm = int.Parse(compact.Groups["m"].Value, CultureInfo.InvariantCulture);
            var cd = int.Parse(compact.Groups["d"].Value, CultureInfo.InvariantCulture);
            if (cy is >= 1990 and <= 2099 && TryCreateDate(cy, cm, cd, out var compactDate))
                return compactDate;
        }

        var yearMatch = Regex.Match(text, @"\b(?<y>19\d{2}|20\d{2})\b");
        if (yearMatch.Success)
        {
            var year = int.Parse(yearMatch.Groups["y"].Value, CultureInfo.InvariantCulture);
            return new DateTime(year, 1, 1);
        }

        return null;
    }

    private static bool TryCreateDate(int year, int month, int day, out DateTime date)
    {
        try
        {
            date = new DateTime(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }
}
