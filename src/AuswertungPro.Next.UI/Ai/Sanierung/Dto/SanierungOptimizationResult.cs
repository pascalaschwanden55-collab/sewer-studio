namespace AuswertungPro.Next.UI.Ai.Sanierung.Dto;

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
