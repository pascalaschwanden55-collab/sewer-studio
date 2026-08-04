using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Services;

public sealed partial class AnnotationWorkbenchService
{
    private static string BuildSourceNote(WorkbenchSourceSuggestion? source)
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
}
