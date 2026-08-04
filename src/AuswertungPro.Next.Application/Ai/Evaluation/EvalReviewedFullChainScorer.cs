using System.Globalization;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record EvalReviewedFullChainPrediction(
    string FrameFileName,
    string PredictedCode,
    int Severity,
    long TimeMs,
    string? Error,
    bool DetectorBypassed,
    bool DinoCalled,
    int DinoBoxCount,
    bool SamCalled,
    int SamMaskCount,
    bool QwenVisionCalled,
    int QwenVisionFindingCount,
    bool CodeMappingCalled,
    int CodeMappingCount,
    TrafficLight? QualityGate,
    double? QualityGateComposite,
    bool Degraded,
    bool Incomplete,
    string? DropReason);

public sealed record EvalReviewedFullChainRow(
    EvalReviewedDamageRow Damage,
    bool DetectorBypassed,
    bool DinoCalled,
    int DinoBoxCount,
    bool SamCalled,
    int SamMaskCount,
    bool QwenVisionCalled,
    int QwenVisionFindingCount,
    bool CodeMappingCalled,
    int CodeMappingCount,
    TrafficLight? QualityGate,
    double? QualityGateComposite,
    bool GatePassed,
    bool Degraded,
    bool Incomplete,
    string? DropReason);

public sealed record EvalReviewedFullChainSummary(
    EvalReviewedDamageSummary Damage,
    int DetectorBypassedFrames,
    int DinoCalledFrames,
    int DinoFramesWithBoxes,
    int SamCalledFrames,
    int SamFramesWithMasks,
    int QwenVisionCalledFrames,
    int QwenVisionFramesWithFindings,
    int CodeMappingCalledFrames,
    int QualityGateEvaluatedFrames,
    int QualityGateGreenFrames,
    int QualityGateYellowFrames,
    int QualityGateRedFrames,
    int DamageFramesPassingGate,
    int FalsePositiveFramesPassingGate,
    int DegradedFrames,
    int IncompleteFrames,
    EvalSetEventScore PresenceEvents,
    EvalSetEventScore ExactCodeEvents);

public sealed record EvalReviewedFullChainScore(
    IReadOnlyList<EvalReviewedFullChainRow> Rows,
    EvalReviewedFullChainSummary Summary);

/// <summary>
/// Misst den vollstaendigen DINO-SAM-Qwen-QualityGate-Weg gegen die menschliche Review.
/// Als bestanden gilt nur ein gruenes QualityGate; Gelb und Rot bleiben review-pflichtig.
/// </summary>
public static class EvalReviewedFullChainScorer
{
    public static EvalReviewedFullChainScore Evaluate(
        IReadOnlyList<EvalReviewedDamageCase> cases,
        IReadOnlyList<EvalReviewedFullChainPrediction> predictions)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(predictions);

        var basicPredictions = predictions
            .Select(item => new EvalSetPrediction(
                item.FrameFileName,
                item.PredictedCode,
                item.Severity,
                item.TimeMs,
                item.Error))
            .ToList();
        var damage = EvalReviewedDamageScorer.Evaluate(cases, basicPredictions);
        var byFrame = predictions.ToDictionary(
            item => item.FrameFileName,
            StringComparer.OrdinalIgnoreCase);
        var rows = damage.Rows.Select(row =>
        {
            byFrame.TryGetValue(row.FrameFileName, out var prediction);
            var gate = prediction?.QualityGate;
            return new EvalReviewedFullChainRow(
                row,
                prediction?.DetectorBypassed == true,
                prediction?.DinoCalled == true,
                prediction?.DinoBoxCount ?? 0,
                prediction?.SamCalled == true,
                prediction?.SamMaskCount ?? 0,
                prediction?.QwenVisionCalled == true,
                prediction?.QwenVisionFindingCount ?? 0,
                prediction?.CodeMappingCalled == true,
                prediction?.CodeMappingCount ?? 0,
                gate,
                prediction?.QualityGateComposite,
                gate == TrafficLight.Green,
                prediction?.Degraded == true,
                prediction?.Incomplete == true,
                prediction?.DropReason);
        }).ToList();

        return new EvalReviewedFullChainScore(rows, Summarize(rows, damage.Summary));
    }

    public static string? DescribeTechnicalError(
        string? primaryError,
        bool incomplete,
        string? degradedReason,
        string? dropReason)
    {
        if (!string.IsNullOrWhiteSpace(primaryError))
            return primaryError;

        var stageError = dropReason switch
        {
            "dino_error" => "DINO-Aufruf fehlgeschlagen (dino_error).",
            "dino_degraded" => "DINO lieferte kein verlaessliches Ergebnis (dino_degraded).",
            "sam_error" => "SAM-Aufruf fehlgeschlagen (sam_error).",
            "vram_insufficient" => "Pipeline wegen VRAM-Mangel unvollstaendig (vram_insufficient).",
            "qwen_timeout" => "Qwen-Vision fehlgeschlagen (qwen_timeout).",
            "qwen_error" => "Qwen-Vision fehlgeschlagen (qwen_error).",
            _ => null
        };

        if (stageError is not null)
        {
            return incomplete && !string.IsNullOrWhiteSpace(degradedReason)
                ? $"{stageError} {degradedReason}"
                : stageError;
        }

        if (!incomplete)
            return null;

        return degradedReason
               ?? (string.IsNullOrWhiteSpace(dropReason) ? null : dropReason)
               ?? "Pipeline unvollstaendig.";
    }

    public static void WriteCsv(
        string path,
        IReadOnlyList<EvalReviewedFullChainRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var text = new StringBuilder();
        text.AppendLine(
            "frame,haltung,event_id,gt_original,gt_reviewed,gt_is_damage,gt_severity," +
            "pred,pred_severity,usable,pred_is_damage,presence_correct,exact_code,main_code," +
            "severity_evaluated,severity_exact,severity_within_one,time_ms,error," +
            "detector_bypassed,dino_called,dino_boxes,sam_called,sam_masks," +
            "qwen_vision_called,qwen_findings,code_mapping_called,code_mapping_count," +
            "quality_gate,quality_gate_composite,gate_passed,degraded,incomplete,drop_reason");

        foreach (var row in rows)
        {
            var damage = row.Damage;
            text.AppendLine(string.Join(",",
                EvalSetCsv.Csv(damage.FrameFileName),
                EvalSetCsv.Csv(damage.HoldingKey),
                EvalSetCsv.Csv(damage.EventId ?? ""),
                EvalSetCsv.Csv(damage.OriginalExpectedCode),
                EvalSetCsv.Csv(damage.ExpectedCode),
                EvalSetCsv.Bool(damage.ExpectedIsDamage),
                damage.ExpectedSeverity?.ToString(CultureInfo.InvariantCulture) ?? "",
                EvalSetCsv.Csv(damage.PredictedCode),
                damage.PredictedSeverity.ToString(CultureInfo.InvariantCulture),
                EvalSetCsv.Bool(damage.HasUsablePrediction),
                EvalSetCsv.Bool(damage.PredictedIsDamage),
                EvalSetCsv.Bool(damage.PresenceCorrect),
                EvalSetCsv.Bool(damage.ExactCodeCorrect),
                EvalSetCsv.Bool(damage.MainCodeCorrect),
                EvalSetCsv.Bool(damage.SeverityEvaluated),
                EvalSetCsv.Bool(damage.SeverityExact),
                EvalSetCsv.Bool(damage.SeverityWithinOne),
                damage.TimeMs.ToString(CultureInfo.InvariantCulture),
                EvalSetCsv.Csv(damage.Error ?? ""),
                EvalSetCsv.Bool(row.DetectorBypassed),
                EvalSetCsv.Bool(row.DinoCalled),
                row.DinoBoxCount.ToString(CultureInfo.InvariantCulture),
                EvalSetCsv.Bool(row.SamCalled),
                row.SamMaskCount.ToString(CultureInfo.InvariantCulture),
                EvalSetCsv.Bool(row.QwenVisionCalled),
                row.QwenVisionFindingCount.ToString(CultureInfo.InvariantCulture),
                EvalSetCsv.Bool(row.CodeMappingCalled),
                row.CodeMappingCount.ToString(CultureInfo.InvariantCulture),
                EvalSetCsv.Csv(row.QualityGate?.ToString() ?? ""),
                row.QualityGateComposite?.ToString("0.000000", CultureInfo.InvariantCulture) ?? "",
                EvalSetCsv.Bool(row.GatePassed),
                EvalSetCsv.Bool(row.Degraded),
                EvalSetCsv.Bool(row.Incomplete),
                EvalSetCsv.Csv(row.DropReason ?? "")));
        }

        AtomicTextFileWriter.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    public static void WriteSummaryJson(
        string path,
        EvalReviewedFullChainSummary summary,
        object metadata)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(new { metadata, summary }, JsonDefaults.Indented);
        AtomicTextFileWriter.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static EvalReviewedFullChainSummary Summarize(
        IReadOnlyList<EvalReviewedFullChainRow> rows,
        EvalReviewedDamageSummary damage)
    {
        var positiveRows = rows.Where(item => item.Damage.ExpectedIsDamage).ToList();
        return new EvalReviewedFullChainSummary(
            damage,
            rows.Count(item => item.DetectorBypassed),
            rows.Count(item => item.DinoCalled),
            rows.Count(item => item.DinoBoxCount > 0),
            rows.Count(item => item.SamCalled),
            rows.Count(item => item.SamMaskCount > 0),
            rows.Count(item => item.QwenVisionCalled),
            rows.Count(item => item.QwenVisionFindingCount > 0),
            rows.Count(item => item.CodeMappingCalled),
            rows.Count(item => item.QualityGate is not null),
            rows.Count(item => item.QualityGate == TrafficLight.Green),
            rows.Count(item => item.QualityGate == TrafficLight.Yellow),
            rows.Count(item => item.QualityGate == TrafficLight.Red),
            positiveRows.Count(item => item.Damage.PredictedIsDamage && item.GatePassed),
            rows.Count(item =>
                !item.Damage.ExpectedIsDamage
                && item.Damage.PredictedIsDamage
                && item.GatePassed),
            rows.Count(item => item.Degraded),
            rows.Count(item => item.Incomplete),
            ScoreEvents(positiveRows, item => item.Damage.PredictedIsDamage),
            ScoreEvents(positiveRows, item => item.Damage.ExactCodeCorrect));
    }

    private static EvalSetEventScore ScoreEvents(
        IReadOnlyList<EvalReviewedFullChainRow> positiveRows,
        Func<EvalReviewedFullChainRow, bool> detected)
    {
        var frameResults = positiveRows.Select(item =>
        {
            var damage = item.Damage;
            var isDetected = detected(item);
            var outcome = !isDetected
                ? EvalSetEventFrameOutcome.NotCorrectlyDetected
                : item.GatePassed
                    ? EvalSetEventFrameOutcome.CorrectlyDetectedGatePassed
                    : EvalSetEventFrameOutcome.CorrectlyDetectedGateBlocked;
            return new EvalSetDamageEventFrameResult(
                damage.FrameFileName,
                damage.HoldingKey,
                damage.EventId
                ?? throw new InvalidDataException(
                    $"Ereignis-ID fehlt fuer {damage.FrameFileName}."),
                damage.ExpectedSeverity
                ?? throw new InvalidDataException(
                    $"Schadensstufe fehlt fuer {damage.FrameFileName}."),
                outcome);
        }).ToList();

        return EvalSetEventScorer.Score(frameResults);
    }
}
