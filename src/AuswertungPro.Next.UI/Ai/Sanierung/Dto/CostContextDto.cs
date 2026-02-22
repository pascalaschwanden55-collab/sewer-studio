namespace AuswertungPro.Next.UI.Ai.Sanierung.Dto;

public sealed record CostContextDto
{
    public string Currency { get; init; } = "CHF";
    public double RegionFactor { get; init; } = 1.0;
    public double InflationFactor { get; init; } = 1.0;
}
