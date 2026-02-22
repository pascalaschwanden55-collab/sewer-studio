namespace AuswertungPro.Next.UI.Ai.Sanierung.Dto;

public sealed record SanierungOptimizationRequest
{
    public required string HaltungId { get; init; }
    public required IReadOnlyList<DamageFindingDto> Findings { get; init; }
    public required PipeContextDto Pipe { get; init; }
    public CostContextDto Cost { get; init; } = new();
    public RuleRecommendationDto? Rule { get; init; }
    public IReadOnlyList<HistoricalCostCaseDto>? SimilarCases { get; init; }
}

public sealed record HistoricalCostCaseDto
{
    public string Code { get; init; } = "";
    public string Measure { get; init; } = "";
    public decimal ActualCost { get; init; }
    public int? PipeDn { get; init; }
    public double? LengthM { get; init; }
}
