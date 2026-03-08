namespace AuswertungPro.Next.Application.Ai.Sanierung;

public sealed record DamageFindingDto
{
    public required string Code { get; init; }
    public string? Quantification { get; init; }
    public double? PositionMeter { get; init; }
    public double? LengthMeter { get; init; }
    public string? SeverityClass { get; init; }
    public string? Comment { get; init; }
}

public sealed record PipeContextDto
{
    public int? DiameterMm { get; init; }
    public string? Material { get; init; }
    public double? LengthMeter { get; init; }
    public double? DepthM { get; init; }
    public AccessDifficulty Access { get; init; } = AccessDifficulty.Medium;
    public bool? Groundwater { get; init; }
    public string? Region { get; init; }
    public int? ProjectYear { get; init; }
}

public enum AccessDifficulty { Easy, Medium, Hard }

public sealed record CostContextDto
{
    public string Currency { get; init; } = "CHF";
    public double RegionFactor { get; init; } = 1.0;
    public double InflationFactor { get; init; } = 1.0;
}

public sealed record RuleRecommendationDto
{
    public IReadOnlyList<string> Measures { get; init; } = [];
    public decimal? EstimatedCost { get; init; }
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public bool UsedTrainedModel { get; init; }
}

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

public sealed record SanierungOptimizationResult
{
    public string RecommendedMeasure { get; init; } = "";
    public double Confidence { get; init; }
    public CostBand CostEstimate { get; init; } = new();
    public string Reasoning { get; init; } = "";
    public IReadOnlyList<string> RiskFlags { get; init; } = [];
    public string UsedSignals { get; init; } = "";
    public bool IsFallback { get; init; }
    public string? Error { get; init; }

    public static SanierungOptimizationResult Empty(string? error = null) => new() { Error = error };
}

public sealed record CostBand
{
    public decimal Min { get; init; }
    public decimal Expected { get; init; }
    public decimal Max { get; init; }
    public string Unit { get; init; } = "CHF";
}
