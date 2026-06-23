using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingMultiModelFindingAddDecisionKind
{
    Add,
    MissingCode,
    DeferSpatial,
    SkipOneTimeDuplicate,
    CoveredExisting
}

public sealed record CodingMultiModelFindingAddDecision(
    CodingMultiModelFindingAddDecisionKind Kind,
    string? Code = null,
    CodingEvent? CoveringEvent = null,
    string? TraceMessage = null);

public static class CodingMultiModelFindingAddDecisionPolicy
{
    public static CodingMultiModelFindingAddDecision Decide(
        string? code,
        string? sourceLabel,
        MetrierungProximityResult proximity,
        LiveFrameFinding finding,
        double meter,
        IEnumerable<CodingEvent>? sessionEvents,
        IEnumerable<CodingEvent> viewEvents)
    {
        ArgumentNullException.ThrowIfNull(proximity);
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(viewEvents);

        if (code == null)
        {
            return new CodingMultiModelFindingAddDecision(
                CodingMultiModelFindingAddDecisionKind.MissingCode,
                TraceMessage: $"[Multi-Model] Kein VSA-Code fuer Label='{sourceLabel}' - uebersprungen");
        }

        if (CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser(code, proximity))
        {
            return new CodingMultiModelFindingAddDecision(
                CodingMultiModelFindingAddDecisionKind.DeferSpatial,
                Code: code,
                TraceMessage: $"[Multi-Model] {code} bei {meter:F2}m nur voraus erkannt - nicht protokolliert");
        }

        if (CodingOneTimeCodeDuplicatePolicy.AlreadyExists(code, sessionEvents, viewEvents))
        {
            return new CodingMultiModelFindingAddDecision(
                CodingMultiModelFindingAddDecisionKind.SkipOneTimeDuplicate,
                Code: code);
        }

        var coveringEvent = CodingFindingCoveragePolicy.FindCoveringEvent(
            viewEvents,
            code,
            meter,
            finding);
        if (coveringEvent != null)
        {
            return new CodingMultiModelFindingAddDecision(
                CodingMultiModelFindingAddDecisionKind.CoveredExisting,
                Code: code,
                CoveringEvent: coveringEvent);
        }

        return new CodingMultiModelFindingAddDecision(
            CodingMultiModelFindingAddDecisionKind.Add,
            Code: code);
    }
}
