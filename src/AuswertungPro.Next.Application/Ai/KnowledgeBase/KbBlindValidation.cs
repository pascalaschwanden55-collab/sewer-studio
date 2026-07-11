using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

/// <summary>Ein Kandidat der blinden KB-Validierung.</summary>
public sealed record KbValidationHit(string CaseId, string VsaCode, double Score, bool? HumanConfirmed);

/// <summary>Ergebnis der blinden Validierung: bestaetigt der KB-Bestand den Code unabhaengig?</summary>
public sealed record KbValidationResult(bool Agrees, KbValidationHit? BestHit, string Reason);

/// <summary>
/// BLINDE KB-Validierung des LLM-Vorschlags (Fehlerpruefung 11.07., Kritisch 1).
///
/// Warum: Die Few-Shot-Beispiele fuer den LLM-Prompt werden mit Vision-Code-Hinweis und
/// Haltungs-ID gesucht — sie koennen das LLM auf denselben Code lenken und danach als
/// "unabhaengige" Bestaetigung erscheinen (Kreisschluss). Dieser Dienst sucht deshalb
/// NACH der LLM-Antwort noch einmal, aber blind: ohne Code-Hinweis, ohne Haltungs-ID.
/// Ein Treffer zaehlt nur, wenn er (a) aus einer FREMDEN Haltung stammt, (b) menschlich
/// bestaetigt ist und (c) den Mindest-Score erreicht.
/// </summary>
public sealed class KbBlindValidationService
{
    /// <summary>Mindest-Aehnlichkeit fuer einen gueltigen Beleg. UNKALIBRIERT — konservativ
    /// gewaehlt; echte Kalibrierung erst nach der Eval-Set-Revision (Stufe 3).</summary>
    public const double MinValidationScore = 0.75;

    private readonly IRetrievalService _retrieval;

    public KbBlindValidationService(IRetrievalService retrieval)
        => _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));

    public async Task<KbValidationResult> ValidateAsync(
        RawVideoDetection detection,
        string requestHaltungId,
        string suggestedCode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suggestedCode))
            return new KbValidationResult(false, null, "Kein Code zum Validieren.");

        var query = BuildBlindQuery(detection);
        var hits = await _retrieval.RetrieveAsync(query, topK: 8, ct).ConfigureAwait(false);

        var kandidaten = hits
            .Select(h => new KbValidationHit(h.Sample.CaseId, h.Sample.VsaCode, h.Score, h.Sample.HumanConfirmed))
            .ToList();

        return EvaluateHits(kandidaten, requestHaltungId, suggestedCode);
    }

    /// <summary>
    /// Suchtext OHNE Haltungs-ID und OHNE jeglichen Code — nur die Beobachtung selbst.
    /// </summary>
    public static string BuildBlindQuery(RawVideoDetection detection)
    {
        var parts = new List<string> { detection.FindingLabel };
        parts.Add($"Meter {detection.MeterStart:0.00}-{detection.MeterEnd:0.00}");
        if (!string.IsNullOrWhiteSpace(detection.Severity))
            parts.Add($"Severity {detection.Severity}");
        if (!string.IsNullOrWhiteSpace(detection.PositionClock))
            parts.Add($"Uhrlage {detection.PositionClock}");
        if (detection.ExtentPercent is > 0)
            parts.Add($"Ausdehnung {detection.ExtentPercent}%");
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>Pure Kernregel — separat testbar.</summary>
    public static KbValidationResult EvaluateHits(
        IReadOnlyList<KbValidationHit> hits,
        string requestHaltungId,
        string suggestedCode)
    {
        if (hits.Count == 0)
            return new KbValidationResult(false, null, "Keine KB-Treffer.");

        var gueltig = hits
            .Where(h => !string.Equals(h.CaseId?.Trim(), requestHaltungId?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(h => h.HumanConfirmed == true)
            .Where(h => h.Score >= MinValidationScore)
            .OrderByDescending(h => h.Score)
            .ToList();

        if (gueltig.Count == 0)
            return new KbValidationResult(false, null,
                "Kein fremder, menschlich bestaetigter Treffer ueber dem Mindest-Score.");

        var best = gueltig[0];
        var agrees = string.Equals(best.VsaCode?.Trim(), suggestedCode.Trim(), StringComparison.OrdinalIgnoreCase);
        return new KbValidationResult(
            agrees,
            best,
            agrees
                ? $"Bestaetigt durch fremden Gold-Fall {best.CaseId} (Score {best.Score:F2})."
                : $"Bester Gold-Treffer sagt {best.VsaCode}, nicht {suggestedCode}.");
    }
}
