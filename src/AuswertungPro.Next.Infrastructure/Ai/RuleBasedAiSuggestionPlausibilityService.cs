using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Deterministische, regelbasierte Plausibilitaetspruefung fuer KI-Codevorschlaege.
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

        if (!isVsaFormat && !isInAllowedCatalog)
        {
            warnings.Add($"PL01: Code '{code}' entspricht nicht dem VSA-Format und ist nicht im Katalog bekannt.");
            confidence = Math.Max(0.0, confidence - 0.5);
            suggestedCode = null;
        }
        else if (!isVsaFormat && isInAllowedCatalog)
        {
            warnings.Add($"PL01: Code '{code}' hat kein klassisches VSA-Format (B[A-H][A-Z]), ist aber im Katalog bekannt.");
            confidence = Math.Max(0.0, confidence - CatalogNonVsaFormatPenalty);
        }
        else if (!isInAllowedCatalog && !isInStaticCatalog)
        {
            warnings.Add($"PL02: Code '{code}' ist nicht im Katalog bekannt.");
            confidence = Math.Max(0.0, confidence - UnknownCodePenalty);
        }

        if (suggestedCode is not null && context.AlreadyConfirmedCodes is not null)
        {
            var baseCode = code.Length >= 3 ? code[..3] : code;
            if (baseCode is "BCD" or "BCE" or "BDC")
            {
                var alreadyExists = false;
                foreach (var confirmed in context.AlreadyConfirmedCodes)
                {
                    if (confirmed.StartsWith(baseCode, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (alreadyExists)
                {
                    var label = baseCode switch
                    {
                        "BCD" => "Rohranfang",
                        "BCE" => "Rohrende",
                        "BDC" => "Abbruch",
                        _ => baseCode
                    };
                    warnings.Add($"PL04: {label} ({baseCode}) wurde bereits erfasst - Duplikat verworfen.");
                    confidence = 0.0;
                    suggestedCode = null;
                }
            }
        }

        if (suggestedCode is not null && context.Observation is not null)
        {
            var obs = context.Observation.ToLowerInvariant();
            var codeInfo = VsaCatalog.Get(code);

            if (codeInfo is not null
                && (obs.Contains("riss") || obs.Contains("crack"))
                && !code.StartsWith("BA"))
            {
                warnings.Add($"PL03: Befund enthaelt 'Riss' aber Code '{code}' ({codeInfo.Label}) ist keine Riss-Kategorie.");
            }

            if (codeInfo is not null
                && (obs.Contains("verformung") || obs.Contains("deformation"))
                && !code.StartsWith("BB"))
            {
                warnings.Add($"PL03: Befund enthaelt 'Verformung' aber Code '{code}' ({codeInfo.Label}) ist keine Verformungs-Kategorie.");
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
