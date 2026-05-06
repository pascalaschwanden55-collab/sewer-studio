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

    /// <summary>Optionale Marktreferenz aus historischen Profil-Aggregaten (Buerglen 2024-2026).
    /// Wird im KI-Prompt als verlaesslicher Kosten-Anker genutzt.</summary>
    public MarktReferenzDto? MarktReferenz { get; init; }

    /// <summary>Hard-Constraint-Filter: Welche Verfahren sind technisch ueberhaupt zulaessig?
    /// Wird VOR der KI-Anfrage von RehabilitationRulesEngine ermittelt.</summary>
    public RulesFilterDto? RulesFilter { get; init; }
}

/// <summary>Vorgefilterte Verfahrensliste aus Hard-Constraint-Engine.</summary>
public sealed record RulesFilterDto
{
    /// <summary>Verfahrens-IDs die laut Regeln definitiv zulaessig sind.</summary>
    public IReadOnlyList<string> EligibleProcedures { get; init; } = Array.Empty<string>();

    /// <summary>Verfahrens-IDs die nur bedingt zulaessig sind (mit Vorbehalten).</summary>
    public IReadOnlyList<string> ConditionalProcedures { get; init; } = Array.Empty<string>();

    /// <summary>Ausgeschlossene Verfahren mit Begruendung (KI darf NICHT vorschlagen).</summary>
    public IReadOnlyList<ExcludedProcedure> ExcludedProcedures { get; init; } = Array.Empty<ExcludedProcedure>();

    /// <summary>Klartext-Hinweise fuer den KI-Prompt (Bogen, AZ etc.).</summary>
    public IReadOnlyList<string> PromptHints { get; init; } = Array.Empty<string>();
}

public sealed record ExcludedProcedure(string ProcedureId, string Name, string Reason);

/// <summary>Aggregat aus realen Sanierungen mit aehnlichem DN/Material/Nutzung-Profil.</summary>
public sealed record MarktReferenzDto
{
    public string ProfilLabel { get; init; } = "";
    public int AnzahlFaelle { get; init; }
    public decimal? KostenProMMedianChf { get; init; }
    public decimal? KostenProHaltungMedianChf { get; init; }
    public IReadOnlyList<string> TypischeMassnahmen { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EmpfohleneSubmissionsBlocks { get; init; } = Array.Empty<string>();
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
