using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingLiveFindingEventWorkflowRequest(
    IReadOnlyList<LiveFrameFinding> ValidFindings,
    double Meter,
    TimeSpan VideoTime,
    ICodingSessionService CodingSessionService,
    IEnumerable<CodingEvent> ViewEvents,
    QualityGateService? QualityGate);

public sealed record CodingLiveFindingEventWorkflowActions(
    Func<LiveFrameFinding, bool> IsFindingTooFarAhead,
    Func<string, string?> LookupVsaLabel,
    Action<ProtocolEntry> AttachAnalyzedFramePhoto,
    Action<string> Trace,
    Action RefreshEvents,
    Action RenderAiOverlays,
    Action TryRenderCurrentOverlay,
    Action UpdateToolBadge,
    Action<CodingEvent, QualityGateResult> PauseAndAskConfirmation);

public sealed record CodingLiveFindingEventWorkflowResult(
    int AddedCount,
    int SkippedCount,
    int CoveredCount,
    CodingEvent? ConfirmationEvent,
    QualityGateResult? ConfirmationGate)
{
    public bool ConfirmationRequested => ConfirmationEvent != null && ConfirmationGate != null;
}

public static class CodingLiveFindingEventWorkflow
{
    public static CodingLiveFindingEventWorkflowResult Execute(
        CodingLiveFindingEventWorkflowRequest request,
        CodingLiveFindingEventWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.ValidFindings);
        ArgumentNullException.ThrowIfNull(request.CodingSessionService);
        ArgumentNullException.ThrowIfNull(request.ViewEvents);

        var addedCount = 0;
        var skippedCount = 0;
        var coveredCount = 0;
        var confirmationTracker = new CodingLiveFindingConfirmationTracker();

        foreach (var finding in request.ValidFindings)
        {
            var code = finding.VsaCodeHint!;
            var addDecision = CodingLiveFindingAddDecisionPolicy.Decide(
                code,
                finding,
                request.Meter,
                actions.IsFindingTooFarAhead(finding),
                request.CodingSessionService.ActiveSession?.Events,
                request.ViewEvents);

            if (addDecision.TraceMessage != null)
                actions.Trace(addDecision.TraceMessage);

            if (addDecision.Kind is CodingLiveFindingAddDecisionKind.SkipTooFarAhead
                or CodingLiveFindingAddDecisionKind.SkipOneTimeDuplicate)
            {
                skippedCount++;
                continue;
            }

            if (addDecision.Kind == CodingLiveFindingAddDecisionKind.CoveredExisting)
            {
                CodingFindingCoveragePolicy.MarkCoveredAgain(addDecision.CoveringEvent!, request.Meter);
                coveredCount++;
                continue;
            }

            var officialLabel = actions.LookupVsaLabel(code);
            var gateResult = CodingLiveFindingQualityGatePolicy.Evaluate(request.QualityGate, finding);

            var draft = CodingLiveFindingEventFactory.Create(
                code,
                officialLabel,
                finding,
                request.Meter,
                request.VideoTime,
                gateResult);

            var codingEvent = CodingLiveFindingSessionAppender.Append(
                draft,
                actions.AttachAnalyzedFramePhoto,
                request.CodingSessionService);

            addedCount++;
            confirmationTracker.Observe(codingEvent, gateResult, finding);
        }

        if (addedCount > 0)
        {
            actions.RefreshEvents();
            actions.RenderAiOverlays();
            actions.TryRenderCurrentOverlay();
            actions.UpdateToolBadge();
        }

        if (confirmationTracker is { Event: not null, Gate: not null })
            actions.PauseAndAskConfirmation(confirmationTracker.Event, confirmationTracker.Gate);

        return new CodingLiveFindingEventWorkflowResult(
            addedCount,
            skippedCount,
            coveredCount,
            confirmationTracker.Event,
            confirmationTracker.Gate);
    }
}
