using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Einheitlicher Sicherheitsweg fuer einen KB-only-Fallback nach LLM-Ausfall.
/// Der Vorschlag wird plausibilisiert, vom Quality Gate bewertet und mit einem
/// Confidence-Deckel versehen. Damit kann ein Infrastrukturfehler nie automatisch
/// als fachlich bestaetigter Befund durchlaufen.
/// </summary>
internal static class KbFallbackMappingPolicy
{
    internal const double MaxFallbackConfidence = 0.84;

    internal static MappedProtocolEntry Build(
        RawVideoDetection detection,
        KbExample fallback,
        string errorMessage,
        IAiSuggestionPlausibilityService plausibility,
        QualityGateService qualityGate)
    {
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(plausibility);
        ArgumentNullException.ThrowIfNull(qualityGate);

        var rawConfidence = Math.Clamp(fallback.Score, 0.35, MaxFallbackConfidence);
        var checkedSuggestion = plausibility.ApplyChecks(
            new AiSuggestionResult(
                SuggestedCode: fallback.Code,
                Confidence: rawConfidence,
                Rationale: "LLM-Fehler, KB-Fallback verwendet.",
                Evidence: fallback.Description,
                Warnings: new[] { "Code-Mapping fehlgeschlagen, KB-Fallback verwendet." }),
            new ObservationContext(detection.FindingLabel));

        var suggestedCode = checkedSuggestion.SuggestedCode;
        var kbAgrees = !string.IsNullOrWhiteSpace(suggestedCode) &&
            fallback.Code.Equals(suggestedCode, StringComparison.OrdinalIgnoreCase);

        var evidence = (detection.Evidence ?? new EvidenceVector()) with
        {
            KbSimilarity = Math.Clamp(fallback.Score, 0.0, 1.0),
            KbCodeAgreement = kbAgrees,
            PlausibilityScore = Math.Clamp(checkedSuggestion.Confidence, 0.0, 1.0),
            DamageCategory = suggestedCode ?? fallback.Code
        };

        var gateResult = qualityGate.Evaluate(evidence);
        var confidence = string.IsNullOrWhiteSpace(suggestedCode)
            ? 0.0
            : Math.Min(gateResult.CompositeConfidence, MaxFallbackConfidence);

        var warnings = new List<string>(checkedSuggestion.Warnings ?? Array.Empty<string>());
        if (string.IsNullOrWhiteSpace(suggestedCode))
            warnings.Add("KB-Fallback wurde von der Plausibilitaetspruefung verworfen.");
        if (!warnings.Any(w => w.Contains("LLM", StringComparison.OrdinalIgnoreCase)))
            warnings.Add("LLM-Aufruf fehlgeschlagen; Ergebnis basiert ausschliesslich auf der Wissensbasis.");

        return new MappedProtocolEntry(
            Detection: detection with { Evidence = evidence },
            SuggestedCode: suggestedCode,
            Confidence: confidence,
            Reason: $"LLM-Fehler, KB-Fallback geprueft: {errorMessage}",
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            QualityGateResult: gateResult,
            Uncertainty: UncertaintyEstimate.FromSinglePass(confidence));
    }
}
