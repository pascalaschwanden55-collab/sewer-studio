using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

public enum CodingLiveFindingAddDecisionKind
{
    Add,
    SkipTooFarAhead,
    SkipOneTimeDuplicate,
    CoveredExisting
}

public sealed record CodingLiveFindingAddDecision(
    CodingLiveFindingAddDecisionKind Kind,
    CodingEvent? CoveringEvent = null,
    string? TraceMessage = null);

public static class CodingLiveFindingAddDecisionPolicy
{
    public static CodingLiveFindingAddDecision Decide(
        string code,
        LiveFrameFinding finding,
        double meter,
        bool isTooFarAhead,
        IEnumerable<CodingEvent>? sessionEvents,
        IEnumerable<CodingEvent> viewEvents)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(viewEvents);

        if (CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead(code, isTooFarAhead))
        {
            return new CodingLiveFindingAddDecision(
                CodingLiveFindingAddDecisionKind.SkipTooFarAhead,
                TraceMessage: $"[Qwen] {code} bei {meter:F2}m nur voraus erkannt (im DN-Kreis) - nicht protokolliert");
        }

        if (CodingOneTimeCodeDuplicatePolicy.AlreadyExists(code, sessionEvents, viewEvents))
        {
            return new CodingLiveFindingAddDecision(
                CodingLiveFindingAddDecisionKind.SkipOneTimeDuplicate,
                TraceMessage: $"[BCD-Dedup] AddFindings: {code} uebersprungen (bereits vorhanden)");
        }

        var coveringEvent = CodingFindingCoveragePolicy.FindCoveringEvent(
            viewEvents,
            code,
            meter,
            finding);
        if (coveringEvent != null)
        {
            return new CodingLiveFindingAddDecision(
                CodingLiveFindingAddDecisionKind.CoveredExisting,
                CoveringEvent: coveringEvent);
        }

        return new CodingLiveFindingAddDecision(CodingLiveFindingAddDecisionKind.Add);
    }
}
