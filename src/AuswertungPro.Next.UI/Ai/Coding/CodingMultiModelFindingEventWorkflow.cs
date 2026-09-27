using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingMultiModelFindingEventWorkflowRequest(
    IReadOnlyList<SegmentedFinding> Segmented,
    IReadOnlyCollection<SegmentedFinding> StretchConsumed,
    double Meter,
    TimeSpan VideoTime,
    double ImageWidth,
    double ImageHeight,
    double? YoloMaxConfidence,
    ICodingSessionService CodingSessionService,
    IEnumerable<CodingEvent> ViewEvents,
    QualityGateService? QualityGate,
    bool MeterFromOsd,
    PipeCalibration? Calibration,
    IVsaCodeSelectionCatalog? CodeSelectionCatalog,
    IReadOnlySet<Guid>? CreatedStretchEventIds = null)
{
    public bool? SameFrameMeterEvidence { get; init; }
}

public sealed record CodingMultiModelFindingEventWorkflowActions(
    Func<LiveFrameFinding, double, string?> ResolveFindingCodeForCoding,
    Func<string, string?> LookupVsaLabel,
    Action<ProtocolEntry> AttachAnalyzedFramePhoto,
    Action<string> Trace,
    Action RefreshEvents,
    Action UpdateToolBadge);

public sealed record CodingMultiModelFindingEventWorkflowResult(
    int AddedCount,
    int SkippedCount,
    int CoveredCount,
    int StretchConsumedCount);

public static class CodingMultiModelFindingEventWorkflow
{
    public static CodingMultiModelFindingEventWorkflowResult Execute(
        CodingMultiModelFindingEventWorkflowRequest request,
        CodingMultiModelFindingEventWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.Segmented);
        ArgumentNullException.ThrowIfNull(request.StretchConsumed);
        ArgumentNullException.ThrowIfNull(request.CodingSessionService);
        ArgumentNullException.ThrowIfNull(request.ViewEvents);

        var addedCount = 0;
        var skippedCount = 0;
        var coveredCount = 0;
        var stretchConsumedCount = 0;
        var improvedCount = 0;

        foreach (var seg in request.Segmented)
        {
            var stretchConsumed = request.StretchConsumed.Contains(seg);
            if (stretchConsumed)
            {
                stretchConsumedCount++;
                if (seg.Origin is null) continue;
            }
            if (seg.Origin is { HasValidEvidence: false }) { skippedCount++; continue; }

            var quant = seg.Quant;
            var dino = seg.Dino;
            var pseudoFinding = CodingSegmentedFindingFrameMapper.Build(
                seg,
                request.ImageWidth,
                request.ImageHeight);

            var code = seg.Origin?.HasYolo == true
                ? CodingLocalizedDetectionPlan.ResolveEventCode(seg.Origin)
                : actions.ResolveFindingCodeForCoding(pseudoFinding, request.Meter);
            if (code is null && stretchConsumed) continue;
            var spatialFollowUp = seg.Origin is
            {
                HasYolo: true,
                HasValidEvidence: true,
                HasTechnicalFailure: false
            } origin
                && CodingLocalizedDetection.IsSha256(origin.YoloArtifactSha256)
                && !string.IsNullOrWhiteSpace(origin.YoloModelName);
            var coverageCandidates = spatialFollowUp
                ? CodingPointFollowUpPolicy.CoverageCandidates(request.CodingSessionService.ActiveSession,
                    request.ViewEvents, code, request.Meter, CodingPointGeometry.FromFinding(pseudoFinding))
                : request.ViewEvents.ToList();
            var addDecision = stretchConsumed
                ? new CodingMultiModelFindingAddDecision(CodingMultiModelFindingAddDecisionKind.Add, code)
                : CodingMultiModelFindingAddDecisionPolicy.Decide(
                code,
                quant.Label,
                seg.Proximity,
                pseudoFinding,
                request.Meter,
                request.CodingSessionService.ActiveSession?.Events,
                coverageCandidates);

            if (addDecision.TraceMessage != null)
                actions.Trace(addDecision.TraceMessage);

            if (addDecision.Kind == CodingMultiModelFindingAddDecisionKind.CoveredExisting)
            {
                coveredCount++;
                if (seg.Origin is null) continue;
            }

            if (addDecision.Kind is not (CodingMultiModelFindingAddDecisionKind.Add
                or CodingMultiModelFindingAddDecisionKind.CoveredExisting))
            {
                skippedCount++;
                continue;
            }

            code = addDecision.Code!;
            var officialLabel = actions.LookupVsaLabel(code);
            var dinoConfidence = dino?.Confidence ?? quant.Confidence;
            var evidence = CodingMultiModelQualityGatePolicy.BuildEvidence(seg, request.YoloMaxConfidence, officialLabel) with
            {
                DamageCategory = code
            };
            var gateResult = CodingMultiModelQualityGatePolicy.Evaluate(
                request.QualityGate,
                evidence,
                requiresReview: seg.Origin?.RequiresReview == true);

            var quantRule = CodingManifestQuantRuleResolver.Resolve(request.CodeSelectionCatalog, code);
            var draft = CodingMultiModelEventFactory.Create(
                code,
                officialLabel,
                seg,
                request.Meter,
                request.VideoTime,
                dinoConfidence,
                gateResult.CompositeConfidence,
                request.ImageWidth,
                request.ImageHeight,
                request.MeterFromOsd,
                request.Calibration,
                quantRule,
                evidence);

            // Audit Fix 3: Ampel aus dem bereits ausgewerteten gateResult in den AiContext uebernehmen.
            draft.AiContext.QualityGateLevel = gateResult.TrafficLight.ToString();
            draft.AiContext.QualityGateWeights = new Dictionary<string, double>(
                gateResult.WeightsUsed,
                StringComparer.Ordinal);
            draft.AiContext.QualityGateExplanation = gateResult.Explanation;

            if (addDecision.CoveringEvent is { } existing)
            {
                var sameFrameMeterEvidence = request.SameFrameMeterEvidence ?? request.MeterFromOsd;
                var block = CodingPointFollowUpPolicy.ImprovementBlockReason(
                    request.CodingSessionService.ActiveSession, existing, draft.Entry, draft.Overlay,
                    draft.AiContext, sameFrameMeterEvidence, coverageCandidates);
                if (block is null)
                {
                    actions.AttachAnalyzedFramePhoto(draft.Entry);
                    if (CodingPointFollowUpPolicy.TryImprove(request.CodingSessionService.ActiveSession,
                        existing, draft.Entry, draft.Overlay, draft.AiContext, sameFrameMeterEvidence,
                        coverageCandidates, out var reason)) improvedCount++;
                    block = reason;
                }
                actions.Trace($"[Folgebeleg] {existing.EventId}: {block}");
                continue;
            }

            if (stretchConsumed)
            {
                // Der Tracker legt die offene Zeile vor diesem Workflow an. Auch
                // diese Zeile braucht den echten Erstbeleg und dieselbe Ampel.
                var open = request.CodingSessionService.Events.FirstOrDefault(e =>
                    request.CreatedStretchEventIds?.Contains(e.EventId) == true
                    && e.Entry.Source == ProtocolEntrySource.Ai && e.Entry.IsStreckenschaden
                    && !e.Entry.IsDeleted && e.ReviewContext is null && e.Overlay is null
                    && e.Entry.MeterEnd is null && e.Entry.MeterStart <= request.Meter
                    && e.AiContext is { Evidence: null, HumanTouchedAtUtc: null, Decision: CodingUserDecision.Ignored }
                    && CodingDedupPolicy.CodesMatch(e.Entry.Code, code));
                if (open is not null)
                {
                    open.AiContext = draft.AiContext;
                    open.Overlay = draft.Overlay;
                    open.Entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = open.Entry.Code };
                    if (draft.Entry.CodeMeta is { } meta)
                    {
                        foreach (var parameter in meta.Parameters)
                        {
                            if (parameter.Key.StartsWith("ai.detector.", StringComparison.Ordinal)
                                || parameter.Key == "ai.code.detail")
                                open.Entry.CodeMeta.Parameters[parameter.Key] = parameter.Value;
                            else open.Entry.CodeMeta.Parameters.TryAdd(parameter.Key, parameter.Value);
                        }
                    }
                    actions.AttachAnalyzedFramePhoto(open.Entry);
                    actions.RefreshEvents();
                }
                continue;
            }

            actions.AttachAnalyzedFramePhoto(draft.Entry);
            CodingMultiModelEventAppender.Apply(draft, request.CodingSessionService);
            addedCount++;
        }

        if (addedCount > 0 || improvedCount > 0)
        {
            actions.RefreshEvents();
            actions.UpdateToolBadge();
        }

        return new CodingMultiModelFindingEventWorkflowResult(
            addedCount,
            skippedCount,
            coveredCount,
            stretchConsumedCount);
    }
}
