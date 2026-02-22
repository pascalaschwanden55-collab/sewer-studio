namespace AuswertungPro.Next.UI.Ai.Sanierung.Dto;

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
