using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingMultiModelFindingEventCommandOutcome
{
    Skipped,
    Executed
}

public sealed record CodingMultiModelFindingEventCommandRequest(
    bool HasCodingViewModel,
    IReadOnlyList<SegmentedFinding> Segmented,
    double ImageWidth,
    double ImageHeight,
    double? YoloMaxConfidence,
    double CaptureTimestampSeconds,
    double? FrameOsdMeter,
    ICodingSessionService? CodingSessionService,
    IEnumerable<CodingEvent> ViewEvents,
    QualityGateService? QualityGate,
    bool MeterFromOsd,
    PipeCalibration? Calibration,
    IVsaCodeSelectionCatalog? CodeSelectionCatalog,
    TimeSpan? CurrentVideoTime = null,
    TimeSpan FallbackVideoTime = default)
{
    public bool? SameFrameMeterEvidence { get; init; }
}

public sealed record CodingMultiModelAnalyzedFrameEventActions(
    Func<double?, double?, double> ResolveMeterForFrame,
    Func<IReadOnlyList<SegmentedFinding>, double, TimeSpan, IReadOnlyCollection<SegmentedFinding>> ApplyStretchTracking,
    Func<LiveFrameFinding, double, string?> ResolveFindingCode,
    Func<string, string?> LookupVsaLabel,
    Action<ProtocolEntry, byte[]> AttachExactFramePhoto,
    Action<string> Trace,
    Action RefreshEvents,
    Action UpdateToolBadge);

public sealed record CodingMultiModelFindingEventCommandActions(
    Func<double?, double?, double> ResolveMeterForFrame,
    Func<IReadOnlyList<SegmentedFinding>, double, TimeSpan, IReadOnlyCollection<SegmentedFinding>> ApplyStretchTracking,
    Func<CodingMultiModelFindingEventWorkflowRequest, CodingMultiModelFindingEventWorkflowResult> ExecuteFindingWorkflow);

public sealed record CodingMultiModelFindingEventCommandResult(
    CodingMultiModelFindingEventCommandOutcome Outcome,
    CodingMultiModelFindingEventWorkflowResult? EventResult,
    double Meter,
    TimeSpan VideoTime);

public static class CodingMultiModelFindingEventCommandWorkflow
{
    /// <summary>Bindet Zeit, OSD und synchrone Fotoablage an denselben Analyseaufruf.</summary>
    public static CodingMultiModelFindingEventCommandResult ExecuteAnalyzedFrame(
        CodingMultiModelFindingEventCommandRequest request,
        byte[] analyzedFrameBytes,
        CodingMultiModelAnalyzedFrameEventActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(analyzedFrameBytes);
        ArgumentNullException.ThrowIfNull(actions);
        var frame = new CodingAnalyzedFrameEvidence(analyzedFrameBytes,
            TimeSpan.FromSeconds(request.CaptureTimestampSeconds),
            actions.ResolveMeterForFrame(request.CaptureTimestampSeconds, request.FrameOsdMeter),
            request.FrameOsdMeter is >= 0 and <= 500);
        return ExecuteAnalyzedFrame(request, frame, actions);
    }

    public static CodingMultiModelFindingEventCommandResult ExecuteAnalyzedFrame(
        CodingMultiModelFindingEventCommandRequest request, CodingAnalyzedFrameEvidence frame,
        CodingMultiModelAnalyzedFrameEventActions actions)
    {
        return Execute(request with
        {
            CurrentVideoTime = frame.CaptureTime,
            MeterFromOsd = frame.MeterFromOsd,
            SameFrameMeterEvidence = frame.HasSameFrameOsd
        }, new CodingMultiModelFindingEventCommandActions(
            (_, _) => frame.Meter,
            actions.ApplyStretchTracking,
            findingRequest => CodingMultiModelFindingEventWorkflow.Execute(findingRequest,
                new CodingMultiModelFindingEventWorkflowActions(actions.ResolveFindingCode,
                    actions.LookupVsaLabel,
                    entry => actions.AttachExactFramePhoto(entry, frame.ImageBytes),
                    actions.Trace, actions.RefreshEvents, actions.UpdateToolBadge))));
    }

    public static CodingMultiModelFindingEventCommandResult Execute(
        CodingMultiModelFindingEventCommandRequest request,
        CodingMultiModelFindingEventCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel || request.CodingSessionService is null)
            return Result(CodingMultiModelFindingEventCommandOutcome.Skipped, null, 0, TimeSpan.Zero);

        ArgumentNullException.ThrowIfNull(request.Segmented);
        ArgumentNullException.ThrowIfNull(request.ViewEvents);

        var meter = actions.ResolveMeterForFrame(
            request.CaptureTimestampSeconds,
            request.FrameOsdMeter);
        var videoTime = request.CurrentVideoTime ?? request.FallbackVideoTime;
        var existingIds = request.CodingSessionService.Events.Select(e => e.EventId).ToHashSet();
        var stretchConsumed = actions.ApplyStretchTracking(
            request.Segmented,
            meter,
            videoTime);

        var eventResult = actions.ExecuteFindingWorkflow(
            new CodingMultiModelFindingEventWorkflowRequest(
                request.Segmented,
                stretchConsumed,
                meter,
                videoTime,
                request.ImageWidth,
                request.ImageHeight,
                request.YoloMaxConfidence,
                request.CodingSessionService,
                request.ViewEvents,
                request.QualityGate,
                request.MeterFromOsd,
                request.Calibration,
                request.CodeSelectionCatalog,
                request.CodingSessionService.Events.Where(e => !existingIds.Contains(e.EventId))
                    .Select(e => e.EventId).ToHashSet()) { SameFrameMeterEvidence = request.SameFrameMeterEvidence });

        return Result(CodingMultiModelFindingEventCommandOutcome.Executed, eventResult, meter, videoTime);
    }

    private static CodingMultiModelFindingEventCommandResult Result(
        CodingMultiModelFindingEventCommandOutcome outcome,
        CodingMultiModelFindingEventWorkflowResult? eventResult,
        double meter,
        TimeSpan videoTime)
        => new(outcome, eventResult, meter, videoTime);
}
