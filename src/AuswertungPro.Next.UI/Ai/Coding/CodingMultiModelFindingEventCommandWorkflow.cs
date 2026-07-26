using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
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
    TimeSpan? CurrentVideoTime,
    TimeSpan FallbackVideoTime);

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
                request.CodeSelectionCatalog));

        return Result(CodingMultiModelFindingEventCommandOutcome.Executed, eventResult, meter, videoTime);
    }

    private static CodingMultiModelFindingEventCommandResult Result(
        CodingMultiModelFindingEventCommandOutcome outcome,
        CodingMultiModelFindingEventWorkflowResult? eventResult,
        double meter,
        TimeSpan videoTime)
        => new(outcome, eventResult, meter, videoTime);
}
