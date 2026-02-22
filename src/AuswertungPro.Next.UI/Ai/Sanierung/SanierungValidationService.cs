using AuswertungPro.Next.UI.Ai.Sanierung.Dto;

namespace AuswertungPro.Next.UI.Ai.Sanierung;

// §8 – Validation Rules (no AI dependency)
public sealed class SanierungValidationService
{
    /// <summary>
    /// Returns true if any finding code starts with "BAD" (Einsturz/Collapse).
    /// </summary>
    public bool IsCollapseDetected(IReadOnlyList<DamageFindingDto> findings)
        => findings.Any(f => f.Code.StartsWith("BAD", StringComparison.OrdinalIgnoreCase));

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
        string? zustandsklasse)
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

        if (constraints.Contains(MeasureConstraint.RequiresStructuralCheck))
            riskFlags.Add("Struktureller Schaden vorhanden – bitte statische Prüfung veranlassen");

        return new ValidationResult
        {
            Passed     = violations.Count == 0,
            Violations = violations,
            RiskFlags  = riskFlags
        };
    }

    private static bool IsLinerMeasure(string measure)
        => measure.Contains("Liner", StringComparison.OrdinalIgnoreCase)
        || measure.Contains("Inliner", StringComparison.OrdinalIgnoreCase);

    private static bool IsReplacementMeasure(string measure)
        => measure.Contains("Erneuerung", StringComparison.OrdinalIgnoreCase)
        || measure.Contains("Neubau", StringComparison.OrdinalIgnoreCase);
}

public enum MeasureConstraint
{
    NoLinerAllowed,
    NoFullReplacement,
    RequiresStructuralCheck
}

public sealed record ValidationResult
{
    public bool Passed { get; init; }
    public IReadOnlyList<MeasureConstraint> Violations { get; init; } = [];
    public IReadOnlyList<string> RiskFlags { get; init; } = [];
}
