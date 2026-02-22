namespace AuswertungPro.Next.UI.Ai.Sanierung.Dto;

public sealed record DamageFindingDto
{
    public required string Code { get; init; }
    public string? Quantification { get; init; }
    public double? PositionMeter { get; init; }
    public double? LengthMeter { get; init; }
    public string? SeverityClass { get; init; }  // e.g. "K1".."K5"
    public string? Comment { get; init; }
}
