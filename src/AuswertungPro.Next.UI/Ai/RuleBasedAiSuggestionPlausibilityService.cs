using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Deterministische, regelbasierte Plausibilitätsprüfung für KI-Codevorschläge.
/// Validiert den vorgeschlagenen VSA-Code gegen den dynamischen Katalog (AllowedCodes)
/// und optional gegen den statischen 24er-Katalog (VsaCatalog) als Fallback.
/// </summary>
public sealed partial class RuleBasedAiSuggestionPlausibilityService : IAiSuggestionPlausibilityService
{
    private const double UnknownCodePenalty = 0.4;
    private const double CatalogNonVsaFormatPenalty = 0.15;

    private readonly IReadOnlySet<string>? _allowedCodes;

    [GeneratedRegex(@"^B[A-H][A-Z]$")]
    private static partial Regex VsaCodePattern();

    public RuleBasedAiSuggestionPlausibilityService() { }

    public RuleBasedAiSuggestionPlausibilityService(IReadOnlySet<string> allowedCodes)
    {
        _allowedCodes = allowedCodes;
    }

    public AiSuggestionResult ApplyChecks(AiSuggestionResult suggestion, ObservationContext context)
    {
        if (string.IsNullOrWhiteSpace(suggestion.SuggestedCode))
            return suggestion;

        var code = suggestion.SuggestedCode.Trim().ToUpperInvariant();
        var warnings = new List<string>(suggestion.Warnings ?? Array.Empty<string>());
        var confidence = suggestion.Confidence;
        string? suggestedCode = code;

        var isInAllowedCatalog = _allowedCodes?.Contains(code) ?? false;
        var isVsaFormat = VsaCodePattern().IsMatch(code);
        var isInStaticCatalog = VsaCatalog.IsKnown(code);

        // PL01: Code-Format prüfen
        if (!isVsaFormat && !isInAllowedCatalog)
        {
            // Weder VSA-Format noch im Katalog → ungültig
            warnings.Add($"PL01: Code '{code}' entspricht nicht dem VSA-Format und ist nicht im Katalog bekannt.");
            confidence = Math.Max(0.0, confidence - 0.5);
            suggestedCode = null;
        }
        else if (!isVsaFormat && isInAllowedCatalog)
        {
            // Im Katalog bekannt aber kein klassisches VSA-Format → Soft-Warning
            warnings.Add($"PL01: Code '{code}' hat kein klassisches VSA-Format (B[A-H][A-Z]), ist aber im Katalog bekannt.");
            confidence = Math.Max(0.0, confidence - CatalogNonVsaFormatPenalty);
        }
        // PL02: Code muss in mindestens einem Katalog bekannt sein
        else if (!isInAllowedCatalog && !isInStaticCatalog)
        {
            warnings.Add($"PL02: Code '{code}' ist nicht im Katalog bekannt.");
            confidence = Math.Max(0.0, confidence - UnknownCodePenalty);
        }

        // PL03: Befund/Code-Mismatch-Heuristik (nur Warning, keine Penalty)
        if (suggestedCode is not null && context.Observation is not null)
        {
            var obs = context.Observation.ToLowerInvariant();
            var codeInfo = VsaCatalog.Get(code);

            if (codeInfo is not null
                && (obs.Contains("riss") || obs.Contains("crack"))
                && !code.StartsWith("BA"))
            {
                warnings.Add($"PL03: Befund enthält 'Riss' aber Code '{code}' ({codeInfo.Label}) ist keine Riss-Kategorie.");
            }

            if (codeInfo is not null
                && (obs.Contains("verformung") || obs.Contains("deformation"))
                && !code.StartsWith("BB"))
            {
                warnings.Add($"PL03: Befund enthält 'Verformung' aber Code '{code}' ({codeInfo.Label}) ist keine Verformungs-Kategorie.");
            }
        }

        return new AiSuggestionResult(
            SuggestedCode: suggestedCode,
            Confidence: confidence,
            Rationale: suggestion.Rationale,
            Evidence: suggestion.Evidence,
            Warnings: warnings.Count > 0 ? warnings.ToArray() : suggestion.Warnings);
    }
}
