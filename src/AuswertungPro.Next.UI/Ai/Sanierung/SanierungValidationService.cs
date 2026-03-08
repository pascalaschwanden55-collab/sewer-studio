using AuswertungPro.Next.Application.Ai.Sanierung;

namespace AuswertungPro.Next.UI.Ai.Sanierung;

// §8 – Validation Rules (no AI dependency)
public sealed class SanierungValidationService
{
    /// <summary>
    /// Returns true if any finding code starts with "BBB" (Einsturz/Collapse).
    /// </summary>
    public bool IsCollapseDetected(IReadOnlyList<DamageFindingDto> findings)
        => findings.Any(f => f.Code.StartsWith("BBB", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns true if Zustandsklasse is Z4 or Z5 (full replacement justified).
    /// </summary>
    public bool IsFullReplacementJustified(string? zustandsklasse)
    {
        var z = (zustandsklasse ?? "").Trim().ToUpperInvariant();
        return z is "Z4" or "Z5" or "4" or "5";
    }

    /// <summary>
    /// Extracts hard constraints from findings and Zustandsklasse.
    /// </summary>
    public IReadOnlyList<MeasureConstraint> ExtractConstraints(
        IReadOnlyList<DamageFindingDto> findings,
        string? zustandsklasse,
        bool groundwater = false,
        int? diameterMm = null)
    {
        var constraints = new List<MeasureConstraint>();

        if (IsCollapseDetected(findings))
            constraints.Add(MeasureConstraint.NoLinerAllowed);

        if (!IsFullReplacementJustified(zustandsklasse))
            constraints.Add(MeasureConstraint.NoFullReplacement);

        var hasStructural = findings.Any(f =>
            f.Code.StartsWith("BAA", StringComparison.OrdinalIgnoreCase) ||
            f.Code.StartsWith("BAB", StringComparison.OrdinalIgnoreCase) ||
            f.Code.StartsWith("BAC", StringComparison.OrdinalIgnoreCase) ||
            f.Code.StartsWith("BAE", StringComparison.OrdinalIgnoreCase));

        if (hasStructural)
            constraints.Add(MeasureConstraint.RequiresStructuralCheck);

        if (groundwater)
            constraints.Add(MeasureConstraint.RequiresDewatering);

        // Berstlining not feasible for large diameters (>DN500)
        if (diameterMm is > 500)
            constraints.Add(MeasureConstraint.NoBerstlining);

        return constraints;
    }

    /// <summary>
    /// Validates a proposed measure against the extracted constraints.
    /// </summary>
    public ValidationResult Validate(string measure, IReadOnlyList<MeasureConstraint> constraints)
    {
        var violations = new List<MeasureConstraint>();
        var riskFlags  = new List<string>();

        if (constraints.Contains(MeasureConstraint.NoLinerAllowed) && IsLinerMeasure(measure))
        {
            violations.Add(MeasureConstraint.NoLinerAllowed);
            riskFlags.Add("Einsturz erkannt – Liner-Verfahren nicht zulässig (§8 NoLinerAllowed)");
        }

        if (constraints.Contains(MeasureConstraint.NoFullReplacement) && IsReplacementMeasure(measure))
        {
            violations.Add(MeasureConstraint.NoFullReplacement);
            riskFlags.Add("Zustandsklasse rechtfertigt keine Vollerneuerung (§8 NoFullReplacement)");
        }

        if (constraints.Contains(MeasureConstraint.NoBerstlining) && IsBerstliningMeasure(measure))
        {
            violations.Add(MeasureConstraint.NoBerstlining);
            riskFlags.Add("DN > 500 – Berstlining nicht zulässig (§8 NoBerstlining)");
        }

        if (constraints.Contains(MeasureConstraint.RequiresStructuralCheck))
            riskFlags.Add("Struktureller Schaden vorhanden – bitte statische Prüfung veranlassen");

        if (constraints.Contains(MeasureConstraint.RequiresDewatering))
            riskFlags.Add("Grundwasser vorhanden – Wasserhaltung erforderlich, Kosten erhöht");

        return new ValidationResult
        {
            Passed     = violations.Count == 0,
            Violations = violations,
            RiskFlags  = riskFlags
        };
    }

    internal static bool IsLinerMeasure(string measure)
        => measure.Contains("Liner", StringComparison.OrdinalIgnoreCase)
        || measure.Contains("Inliner", StringComparison.OrdinalIgnoreCase);

    internal static bool IsReplacementMeasure(string measure)
        => measure.Contains("Erneuerung", StringComparison.OrdinalIgnoreCase)
        || measure.Contains("Neubau", StringComparison.OrdinalIgnoreCase);

    internal static bool IsBerstliningMeasure(string measure)
        => measure.Contains("Berstlining", StringComparison.OrdinalIgnoreCase)
        || measure.Contains("Burst", StringComparison.OrdinalIgnoreCase);
}

public enum MeasureConstraint
{
    NoLinerAllowed,
    NoFullReplacement,
    RequiresStructuralCheck,
    RequiresDewatering,
    NoBerstlining
}

public sealed record ValidationResult
{
    public bool Passed { get; init; }
    public IReadOnlyList<MeasureConstraint> Violations { get; init; } = [];
    public IReadOnlyList<string> RiskFlags { get; init; } = [];
}
