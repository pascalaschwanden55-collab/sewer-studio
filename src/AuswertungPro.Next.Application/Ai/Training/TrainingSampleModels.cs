using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai.Training;

public enum TrainingSampleStatus { New, Approved, Rejected }

/// <summary>KB-Indexierungszustand eines TrainingSamples.</summary>
public enum KbIndexState
{
    /// <summary>Noch nicht durch die KB-Pipeline gelaufen (alte Daten oder neu erzeugt).</summary>
    None = 0,

    /// <summary>Indexierung angefordert, aber noch nicht abgeschlossen.</summary>
    Pending,

    /// <summary>Erfolgreich in knowledge_base.db indexiert.</summary>
    Indexed,

    /// <summary>Indexierung fehlgeschlagen.</summary>
    Error
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
}

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

    /// <summary>Vergleichsergebnis, siehe MatchLevelNames.</summary>
    public string? MatchLevel { get; set; }

    /// <summary>Von der KI erkannter Code.</summary>
    public string? KiCode { get; set; }

    /// <summary>Herkunft des Samples, siehe SourceTypeNames.</summary>
    public string? SourceType { get; set; }

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

    /// <summary>BBox X-Center, normiert 0-1. Null = keine BBox vorhanden.</summary>
    public double? BboxXCenter { get; set; }

    /// <summary>BBox Y-Center, normiert 0-1.</summary>
    public double? BboxYCenter { get; set; }

    /// <summary>BBox Breite, normiert 0-1.</summary>
    public double? BboxWidth { get; set; }

    /// <summary>BBox Hoehe, normiert 0-1.</summary>
    public double? BboxHeight { get; set; }

    /// <summary>Hat eine echte BoundingBox.</summary>
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
