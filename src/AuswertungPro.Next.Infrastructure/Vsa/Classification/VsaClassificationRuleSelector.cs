using System.Globalization;

namespace AuswertungPro.Next.Infrastructure.Vsa.Classification;

public sealed class VsaClassificationRuleSelector
{
    private static readonly string[] Requirements = ["D", "S", "B"];

    private readonly VsaClassificationRuleSet _channels;
    private readonly VsaClassificationRuleSet _manholes;

    public VsaClassificationRuleSelector(
        VsaClassificationRuleSet channels,
        VsaClassificationRuleSet manholes)
    {
        _channels = channels;
        _manholes = manholes;
    }

    public static VsaClassificationRuleSelector Load(string channelsPath, string manholesPath)
        => new(
            VsaClassificationRuleSet.LoadFromFile(channelsPath),
            VsaClassificationRuleSet.LoadFromFile(manholesPath));

    public VsaClassificationOutcome Classify(VsaClassificationRequest request)
    {
        var diagnostics = new List<VsaRuleDiagnostic>();
        var normalizedCode = Normalize(request.Code);
        var ruleSet = ResolveRuleSet(normalizedCode, request.AssetKind);

        VsaRequirementOutcome? d = null;
        VsaRequirementOutcome? s = null;
        VsaRequirementOutcome? b = null;

        var requirements = string.IsNullOrWhiteSpace(request.Requirement)
            ? Requirements
            : [Normalize(request.Requirement)];

        foreach (var requirement in requirements)
        {
            var outcome = ClassifyRequirement(ruleSet, request, normalizedCode, requirement, diagnostics);
            switch (requirement)
            {
                case "D": d = outcome; break;
                case "S": s = outcome; break;
                case "B": b = outcome; break;
            }
        }

        return new VsaClassificationOutcome(normalizedCode, d, s, b, diagnostics);
    }

    private VsaRequirementOutcome? ClassifyRequirement(
        VsaClassificationRuleSet ruleSet,
        VsaClassificationRequest request,
        string normalizedCode,
        string requirement,
        List<VsaRuleDiagnostic> diagnostics)
    {
        var requirementRules = ruleSet.Rules
            .Where(rule => RequirementMatches(rule, requirement))
            .ToList();

        var codeMatches = requirementRules
            .Where(rule => CodeMatches(rule, normalizedCode))
            .ToList();

        if (codeMatches.Count == 0)
        {
            diagnostics.Add(new VsaRuleDiagnostic(normalizedCode, requirement, "rule-not-found",
                "Keine VSA-v2-Regel fuer Code und Anforderung gefunden."));
            return null;
        }

        var ch1Matches = codeMatches
            .Where(rule => CharacterizationMatches(rule.Ch1, request.Ch1))
            .ToList();

        if (ch1Matches.Count == 0)
        {
            diagnostics.Add(BuildCharacterizationDiagnostic(normalizedCode, requirement, "ch1", request.Ch1));
            return null;
        }

        var candidates = ch1Matches
            .Where(rule => CharacterizationMatches(rule.Ch2, request.Ch2))
            .ToList();

        if (candidates.Count == 0)
        {
            diagnostics.Add(BuildCharacterizationDiagnostic(normalizedCode, requirement, "ch2", request.Ch2));
            return null;
        }

        var outcomes = new List<VsaRequirementOutcome>();
        foreach (var rule in candidates)
        {
            var scope = ResolveScope(rule, request, diagnostics);
            if (scope is null)
                continue;

            var outcome = EvaluateRule(rule, request, requirement, scope, diagnostics);
            if (outcome is not null)
                outcomes.Add(outcome);
        }

        if (outcomes.Count == 0)
            return null;

        return outcomes
            .OrderByDescending(outcome => outcome.Specificity)
            .ThenBy(outcome => outcome.RuleId, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static bool RequirementMatches(VsaClassificationRule rule, string requirement)
        => string.Equals(rule.Requirement, requirement, StringComparison.OrdinalIgnoreCase);

    private static bool CodeMatches(VsaClassificationRule rule, string normalizedCode)
    {
        var ruleCode = Normalize(rule.Code);
        if (rule.CodeMatch.Equals("prefix", StringComparison.OrdinalIgnoreCase))
            return normalizedCode.StartsWith(ruleCode, StringComparison.OrdinalIgnoreCase);
        return string.Equals(ruleCode, normalizedCode, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CharacterizationMatches(IReadOnlyCollection<string> allowed, string? value)
    {
        if (allowed.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        return allowed.Contains(Normalize(value), StringComparer.OrdinalIgnoreCase);
    }

    private static VsaRuleDiagnostic BuildCharacterizationDiagnostic(
        string code,
        string requirement,
        string field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new VsaRuleDiagnostic(code, requirement, $"{field}-missing",
                $"{field.ToUpperInvariant()} fehlt fuer die VSA-v2-Regel.");
        }

        return new VsaRuleDiagnostic(code, requirement, $"{field}-unmatched",
            $"{field.ToUpperInvariant()}={Normalize(value)} passt zu keiner VSA-v2-Regel.");
    }

    private static ResolvedScope? ResolveScope(
        VsaClassificationRule rule,
        VsaClassificationRequest request,
        List<VsaRuleDiagnostic> diagnostics)
    {
        var pipeFlexibility = rule.Scope.PipeFlexibility;
        if (pipeFlexibility is "rigid" or "flexible")
        {
            var resolved = VsaMaterialScopeResolver.ResolvePipeFlexibility(request.Material);
            if (resolved is null)
            {
                diagnostics.Add(new VsaRuleDiagnostic(rule.Code, rule.Requirement ?? "", "scope-unresolved",
                    "Rohrmaterial fehlt oder ist nicht auf biegesteif/biegeweich abbildbar."));
                return null;
            }

            if (!string.Equals(resolved, pipeFlexibility, StringComparison.OrdinalIgnoreCase))
                return null;

            return new ResolvedScope(resolved, null, ScopeSpecificity: 2);
        }

        if (rule.Scope.Areas.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(request.Area))
            {
                diagnostics.Add(new VsaRuleDiagnostic(rule.Code, rule.Requirement ?? "", "area-missing",
                    "Schachtbereich fehlt fuer bereichsspezifische Regel."));
                return null;
            }

            var area = Normalize(request.Area);
            if (!rule.Scope.Areas.Contains(area, StringComparer.OrdinalIgnoreCase))
                return null;

            return new ResolvedScope("any", area, ScopeSpecificity: 2);
        }

        return new ResolvedScope("any", null, ScopeSpecificity: 0);
    }

    private static VsaRequirementOutcome? EvaluateRule(
        VsaClassificationRule rule,
        VsaClassificationRequest request,
        string requirement,
        ResolvedScope scope,
        List<VsaRuleDiagnostic> diagnostics)
    {
        if (!rule.Status.Equals("ok", StringComparison.OrdinalIgnoreCase)
            || rule.Classification.Mode.Equals("missing", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new VsaRuleDiagnostic(rule.Code, requirement, rule.Status,
                rule.Classification.Reason ?? "Regel ist nicht produktiv klassifizierbar."));
            return null;
        }

        if (rule.Classification.Mode.Equals("fixed", StringComparison.OrdinalIgnoreCase)
            && rule.Classification.Ez is int fixedEz)
        {
            return BuildOutcome(rule, requirement, fixedEz, scope);
        }

        if (!rule.Classification.Mode.Equals("range", StringComparison.OrdinalIgnoreCase))
            return null;

        var value = ResolveParameterValue(rule.Parameter, request);
        if (value is null)
        {
            diagnostics.Add(new VsaRuleDiagnostic(rule.Code, requirement, "quantification-missing",
                $"Quantifizierung {rule.Parameter} fehlt."));
            return null;
        }

        var range = rule.Classification.Ranges.FirstOrDefault(item => MatchesRange(item, value.Value));
        if (range is null)
        {
            diagnostics.Add(new VsaRuleDiagnostic(rule.Code, requirement, "range-unmatched",
                $"Quantifizierung {value.Value.ToString(CultureInfo.InvariantCulture)} passt zu keiner Schwelle."));
            return null;
        }

        return BuildOutcome(rule, requirement, range.Ez, scope);
    }

    private static VsaRequirementOutcome BuildOutcome(
        VsaClassificationRule rule,
        string requirement,
        int ez,
        ResolvedScope scope)
    {
        var specificity = 1
            + (rule.Ch1.Count > 0 ? 2 : 0)
            + (rule.Ch2.Count > 0 ? 2 : 0)
            + scope.ScopeSpecificity;

        return new VsaRequirementOutcome(
            requirement,
            ez,
            rule.Id,
            rule.SourceRef,
            scope.PipeFlexibility,
            scope.Area,
            specificity);
    }

    private static double? ResolveParameterValue(string parameter, VsaClassificationRequest request)
    {
        var raw = parameter.Equals("q2", StringComparison.OrdinalIgnoreCase) ? request.Q2 : request.Q1;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static bool MatchesRange(VsaClassificationRange range, double value)
    {
        if (range.MinInclusive is double min && value < min)
            return false;
        if (range.MaxExclusive is double max && value >= max)
            return false;
        return true;
    }

    private VsaClassificationRuleSet ResolveRuleSet(string normalizedCode, string? assetKind)
    {
        if (assetKind?.Equals("manhole", StringComparison.OrdinalIgnoreCase) == true)
            return _manholes;
        if (assetKind?.Equals("channel", StringComparison.OrdinalIgnoreCase) == true)
            return _channels;
        return normalizedCode.StartsWith('D') ? _manholes : _channels;
    }

    private static string Normalize(string value)
        => value.Trim().ToUpperInvariant();

    private sealed record ResolvedScope(
        string PipeFlexibility,
        string? Area,
        int ScopeSpecificity);
}

public sealed record VsaClassificationRequest(
    string Code,
    string? Ch1 = null,
    string? Ch2 = null,
    string? Requirement = null,
    string? Q1 = null,
    string? Q2 = null,
    string? Material = null,
    string? Area = null,
    string? AssetKind = null);

public sealed record VsaClassificationOutcome(
    string Code,
    VsaRequirementOutcome? D,
    VsaRequirementOutcome? S,
    VsaRequirementOutcome? B,
    IReadOnlyList<VsaRuleDiagnostic> Diagnostics);

public sealed record VsaRequirementOutcome(
    string Requirement,
    int Ez,
    string RuleId,
    string SourceRef,
    string PipeFlexibility,
    string? Area,
    int Specificity);

public sealed record VsaRuleDiagnostic(
    string Code,
    string Requirement,
    string Reason,
    string Message);

public static class VsaMaterialScopeResolver
{
    private static readonly string[] RigidTokens =
    [
        "BETON",
        "STEINZEUG",
        "GUSS",
        "GUSSEISEN",
        "MAUERWERK",
        "ZEMENT"
    ];

    private static readonly string[] FlexibleTokens =
    [
        "PVC",
        "PE",
        "PP",
        "GFK",
        "KUNSTSTOFF",
        "POLYETHYLEN",
        "POLYPROPYLEN"
    ];

    public static string? ResolvePipeFlexibility(string? material)
    {
        if (string.IsNullOrWhiteSpace(material))
            return null;

        var normalized = material.Trim().ToUpperInvariant();
        if (RigidTokens.Any(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase)))
            return "rigid";
        if (FlexibleTokens.Any(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase)))
            return "flexible";

        return null;
    }
}
