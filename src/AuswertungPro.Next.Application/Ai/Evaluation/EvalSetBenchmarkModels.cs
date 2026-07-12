namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record EvalSetBenchmarkCase(
    string Id,
    string FrameFileName,
    string ImagePath,
    string ExpectedFullCode,
    string ExpectedMainCode,
    string Category,
    double? Meter,
    bool HasYoloLabel = false);

public sealed record EvalSetPrediction(
    string FrameFileName,
    string? PredictedCode,
    int Severity,
    long TimeMs,
    string? Error = null);

public sealed record EvalSetCandidatePrediction(
    string ClassName,
    double Confidence);

public sealed record EvalSetBenchmarkRow(
    string FrameFileName,
    string ExpectedFullCode,
    string ExpectedMainCode,
    string Category,
    string PredictedCode,
    bool Exact,
    bool Main,
    bool Group,
    bool NullResponse,
    bool NegativCorrect,
    long TimeMs,
    int Severity,
    string? Error);

public sealed record EvalSetBenchmarkSummary(
    int Total,
    int ExactCorrect,
    int MainCorrect,
    int GroupCorrect,
    int NullResponses,
    int NegativCorrect,
    double ExactAccuracy,
    double MainAccuracy,
    double GroupAccuracy,
    double NegativeAccuracy,
    double AverageTimeMs);

public sealed record EvalSetCodeSummary(
    string ExpectedCode,
    int Total,
    int ExactCorrect,
    int MainCorrect,
    int GroupCorrect,
    int NullResponses,
    int PredictedLeer,
    double ExactAccuracy,
    string TopPrediction,
    int TopPredictionCount);

public sealed record EvalSetConfusionEntry(
    string ExpectedCode,
    string PredictedCode,
    int Count);

public sealed record EvalSetClassifierCoverageSummary(
    int TotalEvalCases,
    int CoveredEvalCases,
    int MissingEvalCases,
    double CoverageRatio,
    IReadOnlyList<EvalSetClassifierCoverageCode> Codes);

public sealed record EvalSetClassifierCoverageCode(
    string ExpectedCode,
    int Count,
    bool Covered,
    string? CoveredBy);

public sealed record EvalSetRouterClassSummary(
    string RouterClass,
    int Count,
    IReadOnlyList<string> ExpectedCodes);

public sealed record YoloDetectBaselineDetection(
    string ClassName,
    double Confidence);

public sealed record YoloDetectBaselinePrediction(
    string FrameFileName,
    bool IsRelevant,
    IReadOnlyList<YoloDetectBaselineDetection> Detections,
    long RoundtripMs,
    double InferenceTimeMs,
    double QueueWaitMs,
    string? ModelName,
    string? Device,
    double? VramAllocatedGb,
    double? VramTotalGb,
    string? FrameClass,
    string? Error = null,
    string? ModelBackend = null,
    double? GpuUtilizationPercent = null);

public enum YoloDetectNegativeKind
{
    PositiveLabel,
    NoDamage,
    UnlabeledVisibleOrOtherCode
}

public sealed record YoloDetectBaselineRow(
    string FrameFileName,
    string ExpectedFullCode,
    bool ExpectedHasLabel,
    YoloDetectNegativeKind NegativeKind,
    bool Detected,
    int DetectionCount,
    string TopClass,
    double TopConfidence,
    IReadOnlyList<YoloDetectBaselineDetection> Detections,
    long RoundtripMs,
    double InferenceTimeMs,
    double QueueWaitMs,
    string? ModelName,
    string? ModelBackend,
    string? Device,
    double? VramAllocatedGb,
    double? VramTotalGb,
    double? GpuUtilizationPercent,
    string? FrameClass,
    string? Error);

public sealed record YoloDetectFalsePositiveBucket(
    string ClassName,
    string ConfidenceBucket,
    int Count,
    double MaxConfidence,
    double AverageConfidence);

public sealed record YoloDetectBaselineSummary(
    int Total,
    string MetricKind,
    bool IsQualityProof,
    int ExpectedPositiveFrames,
    int ExpectedNegativeFrames,
    int NoDamageNegativeFrames,
    int UnlabeledVisibleOrOtherCodeFrames,
    int DetectedFrames,
    int TruePositiveFrames,
    int FalseNegativeFrames,
    int FalsePositiveFrames,
    int TrueNegativeFrames,
    int TotalDetections,
    double PositiveRecall,
    double Precision,
    double FalsePositiveRate,
    double FalsePositivesPerFrame,
    double AverageRoundtripMs,
    double RoundtripP50Ms,
    double RoundtripP95Ms,
    double AverageInferenceMs,
    double InferenceP50Ms,
    double InferenceP95Ms,
    double AverageQueueWaitMs,
    double? MaxVramAllocatedGb,
    double? MaxVramTotalGb,
    double? MaxGpuUtilizationPercent,
    IReadOnlyList<YoloDetectFalsePositiveBucket> FalsePositiveBuckets);

public sealed record YoloDetectThresholdSummary(
    double ConfidenceThreshold,
    YoloDetectBaselineSummary Summary);
