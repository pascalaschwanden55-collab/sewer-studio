namespace AuswertungPro.Next.UI.Ai.Sanierung.Dto;

public sealed record RuleRecommendationDto
{
    public IReadOnlyList<string> Measures { get; init; } = [];
    public decimal? EstimatedCost { get; init; }
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public bool UsedTrainedModel { get; init; }
}
