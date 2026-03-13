namespace AuswertungPro.Next.Application.Ai;

public sealed record MeasureRecommendationResult(
    IReadOnlyList<string> Measures,
    decimal? EstimatedTotalCost,
    decimal? RenovierungInlinerM,
    int? RenovierungInlinerStk,
    int? AnschluesseVerpressen,
    int? ReparaturManschette,
    int? ReparaturKurzliner,
    int? SimilarCasesCount,
    bool UsedTrainedModel)
{
    public static MeasureRecommendationResult Empty { get; } = new(
        Array.Empty<string>(),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false);
}

public sealed record MeasureLearningStats(
    int TotalSamples,
    int DistinctDamageCodes,
    int CodeSignatures,
    bool TrainedModelAvailable,
    int? TrainedModelSamples,
    DateTime? TrainedAtUtc,
    string ModelPath);

public sealed record MeasureModelTrainingResult(
    bool Trained,
    int TotalSamples,
    int MinSamplesRequired,
    string ModelPath,
    DateTime? TrainedAtUtc,
    string? ErrorMessage);
