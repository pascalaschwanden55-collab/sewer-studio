using System.Globalization;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record EvalReviewedDamageRow(
    string FrameFileName,
    string HoldingKey,
    string? EventId,
    string OriginalExpectedCode,
    string ExpectedCode,
    bool ExpectedIsDamage,
    int? ExpectedSeverity,
    string PredictedCode,
    int PredictedSeverity,
    bool HasUsablePrediction,
    bool PredictedIsDamage,
    bool PresenceCorrect,
    bool ExactCodeCorrect,
    bool MainCodeCorrect,
    bool SeverityEvaluated,
    bool SeverityExact,
    bool SeverityWithinOne,
    long TimeMs,
    string? Error);

public sealed record EvalReviewedDamageEventMetric(
    int EventCount,
    int DetectedEvents,
    EvalSetEventMissStatistics Misses);

public sealed record EvalReviewedDamageSummary(
    int TotalFrames,
    int DamageFrames,
    int NoDamageFrames,
    int TruePositiveDamageFrames,
    int FalseNegativeDamageFrames,
    int FalsePositiveDamageFrames,
    int TrueNegativeDamageFrames,
    int UnresolvedFrames,
    double DamageRecall,
    double DamagePrecision,
    double NoDamageAccuracy,
    int ExactCodeCorrectFrames,
    int MainCodeCorrectFrames,
    double ExactCodeAccuracy,
    double MainCodeAccuracy,
    int SeverityEvaluatedFrames,
    int SeverityExactFrames,
    int SeverityWithinOneFrames,
    double SeverityExactAccuracy,
    double SeverityWithinOneAccuracy,
    EvalReviewedDamageEventMetric PresenceEvents,
    EvalReviewedDamageEventMetric ExactCodeEvents,
    EvalReviewedDamageEventMetric SeverePresenceEvents,
    EvalReviewedDamageEventMetric SevereExactCodeEvents,
    int RequiredSevereEvents,
    bool HasMinimumSevereEvents,
    double AverageTimeMs);

public sealed record EvalReviewedDamageScore(
    IReadOnlyList<EvalReviewedDamageRow> Rows,
    EvalReviewedDamageSummary Summary);

/// <summary>
/// Misst Schadenspraesenz, Code, Stufe und Ereignisse gegen die menschliche Review.
/// Nicht-Schadenscodes auf einem ausgeschlossenen Bild gelten nicht als Schadens-Fehlalarm.
/// </summary>
public static class EvalReviewedDamageScorer
{
    public static EvalReviewedDamageScore Evaluate(
        IReadOnlyList<EvalReviewedDamageCase> cases,
        IReadOnlyList<EvalSetPrediction> predictions)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(predictions);

        var byFrame = predictions.ToDictionary(
            item => item.FrameFileName,
            StringComparer.OrdinalIgnoreCase);
        var rows = new List<EvalReviewedDamageRow>(cases.Count);

        foreach (var reviewedCase in cases)
        {
            var benchmarkCase = reviewedCase.BenchmarkCase;
            byFrame.TryGetValue(benchmarkCase.FrameFileName, out var prediction);
            var predictedCode = EvalSetBenchmarkDataset.NormalizeCode(prediction?.PredictedCode) ?? "";
            var hasUsablePrediction = prediction is not null
                                      && string.IsNullOrWhiteSpace(prediction.Error)
                                      && !string.IsNullOrWhiteSpace(predictedCode);
            var predictedIsDamage = hasUsablePrediction && IsDamageCode(predictedCode);
            var exactCode = reviewedCase.ExpectedIsDamage
                            && predictedIsDamage
                            && string.Equals(
                                predictedCode,
                                benchmarkCase.ExpectedFullCode,
                                StringComparison.OrdinalIgnoreCase);
            var mainCode = reviewedCase.ExpectedIsDamage
                           && predictedIsDamage
                           && SameMainCode(predictedCode, benchmarkCase.ExpectedFullCode);
            var severityEvaluated = reviewedCase.ExpectedIsDamage
                                    && predictedIsDamage
                                    && benchmarkCase.ExpectedSeverity is not null
                                    && prediction!.Severity is >= 1 and <= 5;
            var severityDelta = severityEvaluated
                ? Math.Abs(prediction!.Severity - benchmarkCase.ExpectedSeverity!.Value)
                : int.MaxValue;

            rows.Add(new EvalReviewedDamageRow(
                FrameFileName: benchmarkCase.FrameFileName,
                HoldingKey: benchmarkCase.HoldingKey ?? "",
                EventId: benchmarkCase.EventId,
                OriginalExpectedCode: reviewedCase.OriginalExpectedCode,
                ExpectedCode: reviewedCase.ExpectedIsDamage
                    ? benchmarkCase.ExpectedFullCode
                    : "KEIN_SCHADEN",
                ExpectedIsDamage: reviewedCase.ExpectedIsDamage,
                ExpectedSeverity: benchmarkCase.ExpectedSeverity,
                PredictedCode: predictedCode,
                PredictedSeverity: prediction?.Severity ?? 0,
                HasUsablePrediction: hasUsablePrediction,
                PredictedIsDamage: predictedIsDamage,
                PresenceCorrect: reviewedCase.ExpectedIsDamage
                    ? predictedIsDamage
                    : hasUsablePrediction && !predictedIsDamage,
                ExactCodeCorrect: exactCode,
                MainCodeCorrect: mainCode,
                SeverityEvaluated: severityEvaluated,
                SeverityExact: severityDelta == 0,
                SeverityWithinOne: severityDelta <= 1,
                TimeMs: prediction?.TimeMs ?? 0,
                Error: prediction?.Error));
        }

        return new EvalReviewedDamageScore(rows, Summarize(rows));
    }

    public static void WriteCsv(string path, IReadOnlyList<EvalReviewedDamageRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var text = new StringBuilder();
        text.AppendLine(
            "frame,haltung,event_id,gt_original,gt_reviewed,gt_is_damage,gt_severity," +
            "pred,pred_severity,usable,pred_is_damage,presence_correct,exact_code,main_code," +
            "severity_evaluated,severity_exact,severity_within_one,time_ms,error");

        foreach (var row in rows)
        {
            text.AppendLine(string.Join(",",
                EvalSetCsv.Csv(row.FrameFileName),
                EvalSetCsv.Csv(row.HoldingKey),
                EvalSetCsv.Csv(row.EventId ?? ""),
                EvalSetCsv.Csv(row.OriginalExpectedCode),
                EvalSetCsv.Csv(row.ExpectedCode),
                EvalSetCsv.Bool(row.ExpectedIsDamage),
                row.ExpectedSeverity?.ToString(CultureInfo.InvariantCulture) ?? "",
                EvalSetCsv.Csv(row.PredictedCode),
                row.PredictedSeverity.ToString(CultureInfo.InvariantCulture),
                EvalSetCsv.Bool(row.HasUsablePrediction),
                EvalSetCsv.Bool(row.PredictedIsDamage),
                EvalSetCsv.Bool(row.PresenceCorrect),
                EvalSetCsv.Bool(row.ExactCodeCorrect),
                EvalSetCsv.Bool(row.MainCodeCorrect),
                EvalSetCsv.Bool(row.SeverityEvaluated),
                EvalSetCsv.Bool(row.SeverityExact),
                EvalSetCsv.Bool(row.SeverityWithinOne),
                row.TimeMs.ToString(CultureInfo.InvariantCulture),
                EvalSetCsv.Csv(row.Error ?? "")));
        }

        AtomicTextFileWriter.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    public static void WriteSummaryJson(
        string path,
        EvalReviewedDamageSummary summary,
        object metadata)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(new { metadata, summary }, JsonDefaults.Indented);
        AtomicTextFileWriter.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static EvalReviewedDamageSummary Summarize(
        IReadOnlyList<EvalReviewedDamageRow> rows)
    {
        var positives = rows.Where(item => item.ExpectedIsDamage).ToList();
        var negatives = rows.Where(item => !item.ExpectedIsDamage).ToList();
        var truePositive = positives.Count(item => item.PredictedIsDamage);
        var falseNegative = positives.Count - truePositive;
        var falsePositive = negatives.Count(item => item.PredictedIsDamage);
        var trueNegative = negatives.Count(item =>
            item.HasUsablePrediction && !item.PredictedIsDamage);
        var unresolved = rows.Count(item => !item.HasUsablePrediction);
        var exactCode = positives.Count(item => item.ExactCodeCorrect);
        var mainCode = positives.Count(item => item.MainCodeCorrect);
        var severityRows = positives.Where(item => item.SeverityEvaluated).ToList();

        var presenceEvents = ScoreEvents(positives, item => item.PredictedIsDamage);
        var exactEvents = ScoreEvents(positives, item => item.ExactCodeCorrect);

        return new EvalReviewedDamageSummary(
            TotalFrames: rows.Count,
            DamageFrames: positives.Count,
            NoDamageFrames: negatives.Count,
            TruePositiveDamageFrames: truePositive,
            FalseNegativeDamageFrames: falseNegative,
            FalsePositiveDamageFrames: falsePositive,
            TrueNegativeDamageFrames: trueNegative,
            UnresolvedFrames: unresolved,
            DamageRecall: Ratio(truePositive, positives.Count),
            DamagePrecision: Ratio(truePositive, truePositive + falsePositive),
            NoDamageAccuracy: Ratio(trueNegative, negatives.Count),
            ExactCodeCorrectFrames: exactCode,
            MainCodeCorrectFrames: mainCode,
            ExactCodeAccuracy: Ratio(exactCode, positives.Count),
            MainCodeAccuracy: Ratio(mainCode, positives.Count),
            SeverityEvaluatedFrames: severityRows.Count,
            SeverityExactFrames: severityRows.Count(item => item.SeverityExact),
            SeverityWithinOneFrames: severityRows.Count(item => item.SeverityWithinOne),
            SeverityExactAccuracy: Ratio(
                severityRows.Count(item => item.SeverityExact),
                severityRows.Count),
            SeverityWithinOneAccuracy: Ratio(
                severityRows.Count(item => item.SeverityWithinOne),
                severityRows.Count),
            PresenceEvents: presenceEvents.All,
            ExactCodeEvents: exactEvents.All,
            SeverePresenceEvents: presenceEvents.Severe,
            SevereExactCodeEvents: exactEvents.Severe,
            RequiredSevereEvents: EvalSetEventScorer.RequiredSevereEventCount,
            HasMinimumSevereEvents: presenceEvents.HasMinimumSevereEvents,
            AverageTimeMs: rows.Count == 0 ? 0 : rows.Average(item => item.TimeMs));
    }

    private static EventMetrics ScoreEvents(
        IReadOnlyList<EvalReviewedDamageRow> positiveRows,
        Func<EvalReviewedDamageRow, bool> detected)
    {
        var frameResults = positiveRows.Select(item => new EvalSetDamageEventFrameResult(
            item.FrameFileName,
            item.HoldingKey,
            item.EventId
            ?? throw new InvalidDataException($"Ereignis-ID fehlt fuer {item.FrameFileName}."),
            item.ExpectedSeverity
            ?? throw new InvalidDataException($"Schadensstufe fehlt fuer {item.FrameFileName}."),
            detected(item)
                ? EvalSetEventFrameOutcome.CorrectlyDetectedGateBlocked
                : EvalSetEventFrameOutcome.NotCorrectlyDetected))
            .ToList();
        var score = EvalSetEventScorer.Score(frameResults);

        return new EventMetrics(
            ToMetric(score.AllEvents),
            ToMetric(score.SeverityFourOrFiveEvents),
            score.HasMinimumSeverityFourOrFiveEvents);
    }

    private static EvalReviewedDamageEventMetric ToMetric(
        EvalSetEventOutcomeSummary summary)
        => new(
            summary.EventCount,
            summary.DetectedEvents,
            summary.DetectionMisses);

    private static bool IsDamageCode(string code)
        => code.StartsWith("BA", StringComparison.Ordinal)
           || code.StartsWith("BB", StringComparison.Ordinal);

    private static bool SameMainCode(string predicted, string expected)
        => predicted.Length >= 3
           && expected.Length >= 3
           && string.Equals(predicted[..3], expected[..3], StringComparison.OrdinalIgnoreCase);

    private static double Ratio(int part, int total)
        => total == 0 ? 0 : (double)part / total;

    private sealed record EventMetrics(
        EvalReviewedDamageEventMetric All,
        EvalReviewedDamageEventMetric Severe,
        bool HasMinimumSevereEvents);
}
