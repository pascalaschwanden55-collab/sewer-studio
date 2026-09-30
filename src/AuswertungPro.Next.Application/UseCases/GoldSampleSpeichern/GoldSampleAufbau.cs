using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;

/// <summary>
/// Baut das zu speichernde <see cref="TrainingSample"/> aus Entscheid, Herkunft und
/// gebundenem Goldbild (Feldfolge wie ReviewApprovalService) und uebernimmt die
/// Metadaten eines reparierten Bestandssamples. Frueher
/// <c>AnnotationWorkbenchService.SampleMapping.cs</c>; reine Funktionen ohne Zustand.
/// </summary>
internal static class GoldSampleAufbau
{
    /// <summary>
    /// Einheitliches Abweisungsergebnis: Saved=false, keine SampleId, KbIndexState="-",
    /// kein TeacherAnnotationId. Nur VOR der dauerhaften Speicherung zulaessig.
    /// </summary>
    public static WorkbenchSaveResult Rejected(string message) =>
        new(false, message, null, "-", null);

    public static TrainingSample Erzeuge(
        WorkbenchItem item,
        BoundingBox box,
        WorkbenchDecision decision,
        GoldSampleHerkunft herkunft,
        string sampleId,
        string finalCode,
        string beschreibung,
        string confirmedByUser,
        string storedFramePath,
        bool maskValid)
    {
        var existingSample = herkunft.Bestand;
        var sample = new TrainingSample
        {
            SampleId = sampleId,
            CaseId = item.CaseId,
            Code = finalCode,
            Beschreibung = beschreibung,
            MeterStart = item.MeterStart,
            MeterEnd = item.MeterEnd,
            MeterIsUnknown = item.MeterIsUnknown,
            Signature = TrainingSample.BuildCanonicalSignature(
                item.CaseId,
                finalCode,
                item.MeterStart,
                item.MeterEnd,
                // Mehrfachobjekt: die Hand-Box gehoert zur Objekt-Identitaet — zwei Befunde
                // mit gleichem Code/Meter, aber verschiedenen Boxen sind verschiedene Objekte.
                box.XCenter,
                box.YCenter,
                box.Width,
                box.Height,
                item.MeterIsUnknown),
            Status = maskValid ? TrainingSampleStatus.Approved : TrainingSampleStatus.Draft,
            HumanConfirmed = true,
            Corrected = herkunft.WasCorrected,
            ConfirmedByUser = confirmedByUser,
            ConfirmedAtUtc = DateTime.UtcNow,
            QualityGateLevel = maskValid ? "Green" : "Yellow",
            SourceType = herkunft.SourceType,
            Notes = herkunft.SourceNote,
            SourceReferenceCode = herkunft.SourceReferenceCode,
            SourceReferenceDescription = herkunft.SourceReferenceDescription,
            MatchLevel = herkunft.MatchLevel,
            IsStreckenschaden = existingSample?.IsStreckenschaden ?? item.IsStreckenschaden,
            InspectionDate = existingSample?.InspectionDate
                ?? item.InspectionDate
                ?? item.SourceSuggestion?.InspectionDate,
            FramePath = storedFramePath,
            KbIndexState = KbIndexState.Pending,
        };
        PreserveRepairContext(existingSample, sample);
        ApplyDecisionCodeMeta(sample, finalCode, decision);
        box.ApplyTo(sample);
        return sample;
    }

    public static string BuildSourceNote(WorkbenchSourceSuggestion? source)
    {
        if (source is null)
            return string.Empty;

        var photo = string.IsNullOrWhiteSpace(source.PhotoId)
            ? "-"
            : source.PhotoId;
        return $"PDF-Operateurreferenz: {source.SourceDocumentName}; " +
               $"SHA-256={source.SourceDocumentSha256}; Seite={source.PageNumber}; " +
               $"Foto={photo}; Zuordnung={source.MatchKind}";
    }

    private static void PreserveRepairContext(
        TrainingSample? existing,
        TrainingSample target)
    {
        if (existing is null)
            return;

        target.TimeSeconds = existing.TimeSeconds;
        target.DetectedMeter = existing.DetectedMeter;
        target.MeterSource = existing.MeterSource;
        target.EvidenceFramePath = existing.EvidenceFramePath;
        target.TruthMeterCenter = existing.TruthMeterCenter;
        target.OdsDeltaMeters = existing.OdsDeltaMeters;
        target.HasOsdMismatch = existing.HasOsdMismatch;
        target.FrameIndex = existing.FrameIndex;
        target.KiCode = existing.KiCode;
        target.KbCheck = existing.KbCheck;
        target.CodeMeta = ProtocolRevisionCloner.CloneCodeMeta(existing.CodeMeta);
        target.TechniqueGrade = existing.TechniqueGrade;
        target.AdditionalFramePaths = existing.AdditionalFramePaths?.ToList();
        target.TrainingEligible = existing.TrainingEligible;
        target.TrainingEligibilityReason = existing.TrainingEligibilityReason;
        target.CentralDecision = existing.CentralDecision;
        target.SnapshotError = existing.SnapshotError;
        // ExportedUtc und KbIndexState werden absichtlich nicht uebernommen:
        // eine geaenderte Box/Maske muss erneut exportiert und indexiert werden.
    }

    private static void ApplyDecisionCodeMeta(
        TrainingSample sample,
        string finalCode,
        WorkbenchDecision decision)
    {
        if (sample.CodeMeta is null
            && !decision.ClockPosition.HasValue
            && !decision.Severity.HasValue)
        {
            return;
        }

        sample.CodeMeta ??= new AuswertungPro.Next.Domain.Protocol.ProtocolEntryCodeMeta();
        sample.CodeMeta.Code = finalCode;
        if (decision.ClockPosition.HasValue)
        {
            var clock = decision.ClockPosition.Value;
            var rounded = Math.Round(clock);
            sample.CodeMeta.Parameters["vsa.uhr.von"] = Math.Abs(clock - rounded) < 0.000001
                ? $"{rounded.ToString("0", CultureInfo.InvariantCulture)}:00"
                : clock.ToString("0.##", CultureInfo.InvariantCulture);
            sample.CodeMeta.Parameters.Remove("ClockPos1");
            sample.CodeMeta.Parameters.Remove("Uhr_von");
        }
        if (decision.Severity.HasValue)
        {
            sample.CodeMeta.Severity = decision.Severity.Value.ToString(
                CultureInfo.InvariantCulture);
        }
        sample.CodeMeta.UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ── Kleine reine Helfer ──────────────────────────────────────────────

    public static bool PathsEqual(string? first, string? second)
        => !string.IsNullOrWhiteSpace(first)
           && !string.IsNullOrWhiteSpace(second)
           && string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string? CombineWarnings(params string?[] warnings)
    {
        var present = warnings.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
        return present.Length == 0 ? null : string.Join(" | ", present);
    }

    public static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim().Replace(".", string.Empty).ToUpperInvariant();

    public static DateTimeOffset ToUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero),
        };
}
