using System.Globalization;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public static class YoloDetectBaselineScorer
{
    public static readonly IReadOnlyList<double> DefaultThresholds = [0.25, 0.5, 0.7, 0.85, 0.9];

    public static IReadOnlyList<YoloDetectBaselineRow> Evaluate(
        IReadOnlyList<EvalSetBenchmarkCase> cases,
        IReadOnlyList<YoloDetectBaselinePrediction> predictions,
        double confidenceThreshold = 0)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(predictions);

        var byFrame = predictions.ToDictionary(p => p.FrameFileName, StringComparer.OrdinalIgnoreCase);

        return cases.Select(c =>
        {
            byFrame.TryGetValue(c.FrameFileName, out var prediction);
            var detections = (prediction?.Detections ?? Array.Empty<YoloDetectBaselineDetection>())
                .Where(d => d.Confidence >= confidenceThreshold)
                .OrderByDescending(d => d.Confidence)
                .ToList();
            var top = detections
                .FirstOrDefault();
            var detected = detections.Count > 0;

            return new YoloDetectBaselineRow(
                FrameFileName: c.FrameFileName,
                ExpectedFullCode: c.ExpectedFullCode,
                ExpectedHasLabel: c.HasYoloLabel,
                NegativeKind: ClassifyNegativeKind(c),
                Detected: detected,
                DetectionCount: detections.Count,
                TopClass: top?.ClassName ?? "",
                TopConfidence: top?.Confidence ?? 0,
                Detections: detections,
                RoundtripMs: prediction?.RoundtripMs ?? 0,
                InferenceTimeMs: prediction?.InferenceTimeMs ?? 0,
                QueueWaitMs: prediction?.QueueWaitMs ?? 0,
                ModelName: prediction?.ModelName,
                ModelBackend: prediction?.ModelBackend,
                Device: prediction?.Device,
                VramAllocatedGb: prediction?.VramAllocatedGb,
                VramTotalGb: prediction?.VramTotalGb,
                GpuUtilizationPercent: prediction?.GpuUtilizationPercent,
                FrameClass: prediction?.FrameClass,
                Error: prediction?.Error);
        }).ToList();
    }

    public static IReadOnlyList<YoloDetectThresholdSummary> SweepThresholds(
        IReadOnlyList<EvalSetBenchmarkCase> cases,
        IReadOnlyList<YoloDetectBaselinePrediction> predictions,
        IReadOnlyList<double> confidenceThresholds)
    {
        ArgumentNullException.ThrowIfNull(confidenceThresholds);

        return confidenceThresholds
            .Select(threshold => new YoloDetectThresholdSummary(
                threshold,
                Summarize(Evaluate(cases, predictions, threshold))))
            .ToList();
    }

    public static YoloDetectBaselineSummary Summarize(IReadOnlyList<YoloDetectBaselineRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var total = rows.Count;
        var expectedPositive = rows.Count(r => r.ExpectedHasLabel);
        var expectedNegative = total - expectedPositive;
        var noDamageNegative = rows.Count(r => !r.ExpectedHasLabel && r.NegativeKind == YoloDetectNegativeKind.NoDamage);
        var unlabeledVisibleOrOtherCode = rows.Count(r => !r.ExpectedHasLabel && r.NegativeKind == YoloDetectNegativeKind.UnlabeledVisibleOrOtherCode);
        var detected = rows.Count(r => r.Detected);
        var truePositive = rows.Count(r => r.ExpectedHasLabel && r.Detected);
        var falseNegative = rows.Count(r => r.ExpectedHasLabel && !r.Detected);
        var falsePositive = rows.Count(r => !r.ExpectedHasLabel && r.Detected);
        var trueNegative = rows.Count(r => !r.ExpectedHasLabel && !r.Detected);

        return new YoloDetectBaselineSummary(
            Total: total,
            MetricKind: "presence_health",
            IsQualityProof: false,
            ExpectedPositiveFrames: expectedPositive,
            ExpectedNegativeFrames: expectedNegative,
            NoDamageNegativeFrames: noDamageNegative,
            UnlabeledVisibleOrOtherCodeFrames: unlabeledVisibleOrOtherCode,
            DetectedFrames: detected,
            TruePositiveFrames: truePositive,
            FalseNegativeFrames: falseNegative,
            FalsePositiveFrames: falsePositive,
            TrueNegativeFrames: trueNegative,
            TotalDetections: rows.Sum(r => r.DetectionCount),
            PositiveRecall: Ratio(truePositive, expectedPositive),
            Precision: Ratio(truePositive, detected),
            FalsePositiveRate: Ratio(falsePositive, expectedNegative),
            FalsePositivesPerFrame: Ratio(falsePositive, total),
            AverageRoundtripMs: Average(rows, r => r.RoundtripMs),
            RoundtripP50Ms: Percentile(rows.Select(r => (double)r.RoundtripMs), 0.50),
            RoundtripP95Ms: Percentile(rows.Select(r => (double)r.RoundtripMs), 0.95),
            AverageInferenceMs: Average(rows, r => r.InferenceTimeMs),
            InferenceP50Ms: Percentile(rows.Select(r => r.InferenceTimeMs), 0.50),
            InferenceP95Ms: Percentile(rows.Select(r => r.InferenceTimeMs), 0.95),
            AverageQueueWaitMs: Average(rows, r => r.QueueWaitMs),
            MaxVramAllocatedGb: MaxNullable(rows.Select(r => r.VramAllocatedGb)),
            MaxVramTotalGb: MaxNullable(rows.Select(r => r.VramTotalGb)),
            MaxGpuUtilizationPercent: MaxNullable(rows.Select(r => r.GpuUtilizationPercent)),
            FalsePositiveBuckets: BuildFalsePositiveBuckets(rows));
    }

    public static void WriteCsv(string path, IReadOnlyList<YoloDetectBaselineRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var sb = new StringBuilder();
        sb.AppendLine("frame,expected_code,expected_has_label,negative_kind,detected,detection_count,top_class,top_confidence,roundtrip_ms,inference_ms,queue_wait_ms,model_name,model_backend,device,vram_allocated_gb,vram_total_gb,gpu_utilization_percent,frame_class,error");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(r.FrameFileName),
                Csv(r.ExpectedFullCode),
                Bool(r.ExpectedHasLabel),
                Csv(r.NegativeKind.ToString()),
                Bool(r.Detected),
                r.DetectionCount.ToString(CultureInfo.InvariantCulture),
                Csv(r.TopClass),
                r.TopConfidence.ToString(CultureInfo.InvariantCulture),
                r.RoundtripMs.ToString(CultureInfo.InvariantCulture),
                r.InferenceTimeMs.ToString(CultureInfo.InvariantCulture),
                r.QueueWaitMs.ToString(CultureInfo.InvariantCulture),
                Csv(r.ModelName ?? ""),
                Csv(r.ModelBackend ?? ""),
                Csv(r.Device ?? ""),
                NullableDouble(r.VramAllocatedGb),
                NullableDouble(r.VramTotalGb),
                NullableDouble(r.GpuUtilizationPercent),
                Csv(r.FrameClass ?? ""),
                Csv(r.Error ?? "")));
        }
        AtomicTextFileWriter.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteSweepCsv(string path, IReadOnlyList<YoloDetectThresholdSummary> sweep)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var sb = new StringBuilder();
        sb.AppendLine("threshold,total,expected_positive,expected_negative,no_damage_negative,unlabeled_visible_or_other_code,detected_frames,true_positive,false_negative,false_positive,true_negative,total_detections,recall,precision,fp_per_frame,fp_rate,avg_roundtrip_ms,p50_roundtrip_ms,p95_roundtrip_ms,avg_inference_ms,p50_inference_ms,p95_inference_ms,avg_queue_wait_ms,max_vram_allocated_gb,max_vram_total_gb,max_gpu_utilization_percent");
        foreach (var s in sweep)
        {
            var r = s.Summary;
            sb.AppendLine(string.Join(",",
                s.ConfidenceThreshold.ToString(CultureInfo.InvariantCulture),
                r.Total.ToString(CultureInfo.InvariantCulture),
                r.ExpectedPositiveFrames.ToString(CultureInfo.InvariantCulture),
                r.ExpectedNegativeFrames.ToString(CultureInfo.InvariantCulture),
                r.NoDamageNegativeFrames.ToString(CultureInfo.InvariantCulture),
                r.UnlabeledVisibleOrOtherCodeFrames.ToString(CultureInfo.InvariantCulture),
                r.DetectedFrames.ToString(CultureInfo.InvariantCulture),
                r.TruePositiveFrames.ToString(CultureInfo.InvariantCulture),
                r.FalseNegativeFrames.ToString(CultureInfo.InvariantCulture),
                r.FalsePositiveFrames.ToString(CultureInfo.InvariantCulture),
                r.TrueNegativeFrames.ToString(CultureInfo.InvariantCulture),
                r.TotalDetections.ToString(CultureInfo.InvariantCulture),
                r.PositiveRecall.ToString(CultureInfo.InvariantCulture),
                r.Precision.ToString(CultureInfo.InvariantCulture),
                r.FalsePositivesPerFrame.ToString(CultureInfo.InvariantCulture),
                r.FalsePositiveRate.ToString(CultureInfo.InvariantCulture),
                r.AverageRoundtripMs.ToString(CultureInfo.InvariantCulture),
                r.RoundtripP50Ms.ToString(CultureInfo.InvariantCulture),
                r.RoundtripP95Ms.ToString(CultureInfo.InvariantCulture),
                r.AverageInferenceMs.ToString(CultureInfo.InvariantCulture),
                r.InferenceP50Ms.ToString(CultureInfo.InvariantCulture),
                r.InferenceP95Ms.ToString(CultureInfo.InvariantCulture),
                r.AverageQueueWaitMs.ToString(CultureInfo.InvariantCulture),
                NullableDouble(r.MaxVramAllocatedGb),
                NullableDouble(r.MaxVramTotalGb),
                NullableDouble(r.MaxGpuUtilizationPercent)));
        }
        AtomicTextFileWriter.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteSummaryJson(
        string path,
        YoloDetectBaselineSummary summary,
        object metadata,
        IReadOnlyList<YoloDetectThresholdSummary>? thresholdSweep = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(new
        {
            metadata,
            summary,
            threshold_sweep = thresholdSweep ?? Array.Empty<YoloDetectThresholdSummary>()
        }, JsonDefaults.Indented);
        AtomicTextFileWriter.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static YoloDetectNegativeKind ClassifyNegativeKind(EvalSetBenchmarkCase c)
    {
        if (c.HasYoloLabel)
            return YoloDetectNegativeKind.PositiveLabel;

        return IsNoDamageCode(c.ExpectedFullCode) || IsNoDamageCode(c.ExpectedMainCode)
            ? YoloDetectNegativeKind.NoDamage
            : YoloDetectNegativeKind.UnlabeledVisibleOrOtherCode;
    }

    private static bool IsNoDamageCode(string code)
        => string.Equals(code, "LEER", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(code, "KEIN_SCHADEN", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<YoloDetectFalsePositiveBucket> BuildFalsePositiveBuckets(
        IReadOnlyList<YoloDetectBaselineRow> rows)
        => rows
            .Where(r => !r.ExpectedHasLabel && r.Detected)
            .SelectMany(r => r.Detections)
            .GroupBy(d => new { ClassName = d.ClassName, Bucket = ConfidenceBucket(d.Confidence) })
            .OrderBy(g => g.Key.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(g => g.Max(d => d.Confidence))
            .Select(g => new YoloDetectFalsePositiveBucket(
                g.Key.ClassName,
                g.Key.Bucket,
                g.Count(),
                g.Max(d => d.Confidence),
                g.Average(d => d.Confidence)))
            .ToList();

    private static string ConfidenceBucket(double confidence)
        => confidence switch
        {
            >= 0.9 => ">=0.90",
            >= 0.85 => "0.85-0.89",
            >= 0.7 => "0.70-0.84",
            >= 0.5 => "0.50-0.69",
            >= 0.25 => "0.25-0.49",
            _ => "<0.25"
        };

    private static double Ratio(int part, int total)
        => total == 0 ? 0 : (double)part / total;

    private static double Average<T>(IReadOnlyList<T> rows, Func<T, double> selector)
        => rows.Count == 0 ? 0 : rows.Average(selector);

    private static double Average<T>(IReadOnlyList<T> rows, Func<T, long> selector)
        => rows.Count == 0 ? 0 : rows.Average(selector);

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
            return 0;
        if (sorted.Length == 1)
            return Math.Round(sorted[0], 1);

        var index = percentile * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        var fraction = index - lower;
        return Math.Round(sorted[lower] + fraction * (sorted[upper] - sorted[lower]), 1);
    }

    private static double? MaxNullable(IEnumerable<double?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : present.Max();
    }

    // Delegiert an gemeinsame Helferklasse EvalSetCsv
    private static string Bool(bool value) => EvalSetCsv.Bool(value);

    private static string NullableDouble(double? value)
        => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "";

    // Delegiert an gemeinsame Helferklasse EvalSetCsv
    private static string Csv(string value) => EvalSetCsv.Csv(value);
}
