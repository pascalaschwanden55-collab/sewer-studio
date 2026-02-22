using AuswertungPro.Next.UI.Ai.Sanierung.Dto;

namespace AuswertungPro.Next.UI.Ai.Sanierung;

public sealed class CostOptimizationEngine
{
    // Base unit costs in CHF/m (for linear measures) or CHF/Stk (for point measures)
    private static readonly Dictionary<string, (decimal UnitCost, bool IsPerMeter)> BaseCosts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Schlauchliner"]           = (180m, true),
            ["Inliner"]                 = (180m, true),
            ["Liner"]                   = (180m, true),
            ["Kurzliner"]               = (950m, false),
            ["Reparatur"]               = (650m, false),
            ["Roboter"]                 = (500m, false),
            ["Erneuerung"]              = (600m, true),
            ["Neubau"]                  = (600m, true),
            ["Renovation"]              = (300m, true),
            ["Sanierung"]               = (250m, true),
        };

    public CostBand Calculate(CostCalcInput input)
    {
        var (baseUnit, isPerMeter) = ResolveBase(input.Measure);

        var dnFactor = input.DiameterMm switch
        {
            < 300  => 1.00m,
            < 400  => 1.20m,
            < 500  => 1.50m,
            _      => 2.00m
        };

        var depthFactor = input.DepthM switch
        {
            < 3.0  => 1.00m,
            < 5.0  => 1.10m,
            _      => 1.25m
        };

        var materialFactor = (input.Material ?? "").Trim().ToUpperInvariant() switch
        {
            "PVC" or "PE" or "PP" => 0.90m,
            "GFK"                 => 0.95m,
            "STEINZEUG"           => 1.05m,
            _                     => 1.00m   // Beton, other
        };

        var accessFactor = input.Access switch
        {
            AccessDifficulty.Easy  => 1.00m,
            AccessDifficulty.Hard  => 1.35m,
            _                      => 1.15m  // Medium
        };

        var regionFactor   = (decimal)input.RegionFactor;
        var correctionFactor = (decimal)input.AiCorrectionFactor;
        var length         = (decimal)(input.LengthMeter ?? 1.0);

        var unitCostAdjusted = baseUnit * dnFactor * depthFactor * materialFactor * accessFactor
                                        * regionFactor * correctionFactor;

        var expected = isPerMeter ? unitCostAdjusted * length : unitCostAdjusted;
        var min      = Math.Round(expected * 0.85m, 0);
        var max      = Math.Round(expected * 1.20m, 0);
        expected     = Math.Round(expected, 0);

        return new CostBand { Min = min, Expected = expected, Max = max, Unit = "CHF" };
    }

    private static (decimal UnitCost, bool IsPerMeter) ResolveBase(string measure)
    {
        var m = (measure ?? "").Trim();
        foreach (var kv in BaseCosts)
        {
            if (m.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }
        // fallback: generic sanierung per meter
        return (200m, true);
    }
}

public sealed record CostCalcInput
{
    public string Measure { get; init; } = "";
    public int? DiameterMm { get; init; }
    public double? LengthMeter { get; init; }
    public double? DepthM { get; init; }
    public AccessDifficulty Access { get; init; } = AccessDifficulty.Medium;
    public string? Material { get; init; }
    public double RegionFactor { get; init; } = 1.0;
    public double AiCorrectionFactor { get; init; } = 1.0;
}
