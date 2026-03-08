using AuswertungPro.Next.Application.Ai.Sanierung;

namespace AuswertungPro.Next.UI.Ai.Sanierung;

public sealed class CostOptimizationEngine
{
    // Base unit costs in CHF/m (for linear measures) or CHF/Stk (for point measures)
    // Quellen: SIA 190, VSA-Richtlinie, CH-Marktpreise 2024/2025
    private static readonly Dictionary<string, (decimal UnitCost, bool IsPerMeter)> BaseCosts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Liner-Verfahren (pro Meter)
            ["Schlauchliner"]           = (180m, true),
            ["Inliner"]                 = (180m, true),
            ["Liner"]                   = (180m, true),
            ["GFK-Liner"]               = (220m, true),
            ["UV-Liner"]                = (200m, true),
            ["Kurzliner"]               = (950m, false),
            // Punktuelle Reparaturen (pro Stück)
            ["Reparatur"]               = (650m, false),
            ["Roboter"]                 = (500m, false),
            ["Robotersanierung"]        = (520m, false),
            ["Manschette"]              = (420m, false),
            ["Verpressen"]              = (280m, false),
            ["Injektionsverfahren"]     = (350m, false),
            ["Hutprofil"]               = (700m, false),
            ["Anschluss"]               = (380m, false),
            // Erneuerung / Neubau (pro Meter)
            ["Erneuerung"]              = (600m, true),
            ["Neubau"]                  = (600m, true),
            ["Vollerneuerung"]          = (650m, true),
            // Renovation (pro Meter)
            ["Renovation"]              = (300m, true),
            ["Sanierung"]               = (250m, true),
            ["Berstlining"]             = (350m, true),
            ["Rohrstrang"]              = (400m, true),
            // Grabenlose Verfahren
            ["Relining"]                = (280m, true),
            ["Pipe-Eating"]             = (500m, true),
        };

    public CostBand Calculate(CostCalcInput input)
    {
        var (baseUnit, isPerMeter) = ResolveBase(input.Measure);

        // Feinere DN-Abstufung (realistischer für CH-Markt)
        var dn = input.DiameterMm ?? 300;
        var dnFactor = dn switch
        {
            <= 150 => 0.95m,
            <= 200 => 1.00m,
            <= 250 => 1.05m,
            <= 300 => 1.10m,
            <= 400 => 1.25m,
            <= 500 => 1.50m,
            <= 600 => 1.80m,
            _      => 2.00m + (dn - 600) * 0.002m  // linear above DN600
        };

        var depthFactor = input.DepthM switch
        {
            null   => 1.00m,
            < 2.0  => 0.95m,
            < 3.0  => 1.00m,
            < 5.0  => 1.10m,
            < 7.0  => 1.25m,
            _      => 1.40m   // very deep
        };

        var materialFactor = (input.Material ?? "").Trim().ToUpperInvariant() switch
        {
            "PVC" or "PE" or "PP" => 0.90m,
            "GFK"                 => 0.95m,
            "STEINZEUG"           => 1.05m,
            "GUSS"                => 1.10m,
            _                     => 1.00m   // Beton, other
        };

        var accessFactor = input.Access switch
        {
            AccessDifficulty.Easy  => 1.00m,
            AccessDifficulty.Hard  => 1.35m,
            _                      => 1.15m  // Medium
        };

        // Grundwasser-Zuschlag (Wasserhaltung nötig)
        var groundwaterFactor = input.Groundwater ? 1.15m : 1.00m;

        var regionFactor     = (decimal)Math.Clamp(input.RegionFactor, 0.5, 2.0);
        var inflationFactor  = (decimal)Math.Clamp(input.InflationFactor, 0.8, 1.5);
        var correctionFactor = (decimal)Math.Clamp(input.AiCorrectionFactor, 0.5, 2.0);
        var length           = (decimal)Math.Max(input.LengthMeter ?? 1.0, 0.1);

        var unitCostAdjusted = baseUnit * dnFactor * depthFactor * materialFactor * accessFactor
                                        * groundwaterFactor * regionFactor * inflationFactor
                                        * correctionFactor;

        var expected = isPerMeter ? unitCostAdjusted * length : unitCostAdjusted;
        var min      = Math.Round(expected * 0.85m, 0);
        var max      = Math.Round(expected * 1.20m, 0);
        expected     = Math.Round(expected, 0);

        return new CostBand { Min = min, Expected = expected, Max = max, Unit = "CHF" };
    }

    private static (decimal UnitCost, bool IsPerMeter) ResolveBase(string measure)
    {
        var m = (measure ?? "").Trim();

        // Exakter Match zuerst
        foreach (var kv in BaseCosts)
        {
            if (m.Equals(kv.Key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        // Teilstring-Match
        foreach (var kv in BaseCosts)
        {
            if (m.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        // Intelligenter Fallback nach Kategorie
        if (m.Contains("Erneuerung", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Neubau", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Ersatz", StringComparison.OrdinalIgnoreCase))
            return (600m, true);

        if (m.Contains("Liner", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Inliner", StringComparison.OrdinalIgnoreCase))
            return (180m, true);

        if (m.Contains("Reparatur", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Roboter", StringComparison.OrdinalIgnoreCase))
            return (550m, false);

        System.Diagnostics.Debug.WriteLine(
            $"[CostEngine] Unbekannte Massnahme '{measure}' – verwende generischen Fallback 300 CHF/m");

        // Konservativer Fallback (Mittelwert statt Minimum)
        return (300m, true);
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
    public bool Groundwater { get; init; }
    public double RegionFactor { get; init; } = 1.0;
    public double InflationFactor { get; init; } = 1.0;
    public double AiCorrectionFactor { get; init; } = 1.0;
}
