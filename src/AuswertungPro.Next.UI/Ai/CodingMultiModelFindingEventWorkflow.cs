using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai;

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
    IVsaCodeSelectionCatalog? CodeSelectionCatalog);

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

        foreach (var seg in request.Segmented)
        {
            if (request.StretchConsumed.Contains(seg))
            {
                stretchConsumedCount++;
                continue;
            }

            var quant = seg.Quant;
            var dino = seg.Dino;
            var pseudoFinding = CodingSegmentedFindingFrameMapper.Build(
                seg,
                request.ImageWidth,
                request.ImageHeight);

            var code = actions.ResolveFindingCodeForCoding(pseudoFinding, request.Meter);
            var addDecision = CodingMultiModelFindingAddDecisionPolicy.Decide(
                code,
                quant.Label,
                seg.Proximity,
                pseudoFinding,
                request.Meter,
                request.CodingSessionService.ActiveSession?.Events,
                request.ViewEvents);
            if (addDecision.TraceMessage != null)
                actions.Trace(addDecision.TraceMessage);

            if (addDecision.Kind == CodingMultiModelFindingAddDecisionKind.CoveredExisting)
            {
                coveredCount++;
                continue;
            }

            if (addDecision.Kind != CodingMultiModelFindingAddDecisionKind.Add)
            {
                skippedCount++;
                continue;
            }

            code = addDecision.Code!;
            var officialLabel = actions.LookupVsaLabel(code);
            var dinoConfidence = dino?.Confidence ?? quant.Confidence;
            var gateResult = CodingMultiModelQualityGatePolicy.Evaluate(
                request.QualityGate,
                request.YoloMaxConfidence,
                dinoConfidence,
                quant.Confidence,
                officialLabel);

            var quantRule = CodingManifestQuantRuleResolver.Resolve(request.CodeSelectionCatalog, code);
            var draft = CodingMultiModelEventFactory.Create(
                code,
                officialLabel,
                seg,
                request.Meter,
                request.VideoTime,
                dinoConfidence,
                gateResult,
                request.ImageWidth,
                request.ImageHeight,
                request.MeterFromOsd,
                request.Calibration,
                quantRule);

            actions.AttachAnalyzedFramePhoto(draft.Entry);
            CodingMultiModelEventAppender.Apply(draft, request.CodingSessionService);
            addedCount++;
        }

        if (addedCount > 0)
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
