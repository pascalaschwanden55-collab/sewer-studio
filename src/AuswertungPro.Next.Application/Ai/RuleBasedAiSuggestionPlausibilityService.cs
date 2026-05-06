using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Ai;

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

        // PL04: Steuercodes die nur 1× pro Haltung vorkommen duerfen
        // BCD=Rohranfang, BCE=Rohrende, BDC=Abbruch — Duplikate mit niedrigerer Konfidenz verwerfen
        if (suggestedCode is not null && context.AlreadyConfirmedCodes is not null)
        {
            var baseCode = code.Length >= 3 ? code[..3] : code;
            if (baseCode is "BCD" or "BCE" or "BDC")
            {
                // Pruefen ob dieser Steuercode bereits bestaetigt wurde
                bool alreadyExists = false;
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
                    warnings.Add($"PL04: {label} ({baseCode}) wurde bereits erfasst — Duplikat verworfen.");
                    confidence = 0.0;
                    suggestedCode = null;
                }
            }
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

            // BAA = Verformung/Deformation (VSA 2018), NICHT BB-Gruppe
            if (codeInfo is not null
                && (obs.Contains("verformung") || obs.Contains("deformation"))
                && !code.StartsWith("BAA", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"PL03: Befund enthält 'Verformung' aber Code '{code}' ({codeInfo.Label}) ist keine Verformungs-Kategorie (BAA erwartet).");
            }
        }

        // PL05: Characterization/Quantification-Pruefung
        // Wenn der VSA-Code eine Charakterisierung erfordert (z.B. BAB braucht A-E),
        // aber der Code nur 3 Zeichen hat → Penalty (unvollstaendig)
        if (suggestedCode is not null && code.Length >= 3)
        {
            var catalogEntry = VsaCatalog.Get(code[..3]);
            if (catalogEntry is not null)
            {
                if (catalogEntry.RequiresCharacterization && code.Length <= 3)
                {
                    warnings.Add($"PL05: Code '{code}' erfordert Charakterisierung (Char1 fehlt).");
                    confidence = Math.Max(0.0, confidence - 0.10);
                }
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
