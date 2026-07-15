using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingAnalysisContext
{
    private readonly Func<IEnumerable<CodingEvent>?> _sessionEvents;
    private readonly Func<IEnumerable<CodingEvent>?> _viewEvents;
    private readonly Func<IEnumerable<CodingEvent>> _importEvents;
    private readonly Func<PipeCalibration?> _calibration;
    private readonly Func<double> _videoAspect;
    private readonly Func<SingleFrameResult, PipeCalibration?, IReadOnlyList<SegmentedFinding>> _buildSegmentedFindings;
    private readonly Func<CancellationToken, Task<byte[]?>> _captureSnapshotAsync;

    public CodingAnalysisContext(
        Func<IEnumerable<CodingEvent>?> sessionEvents,
        Func<IEnumerable<CodingEvent>?> viewEvents,
        Func<IEnumerable<CodingEvent>> importEvents,
        Func<PipeCalibration?> calibration,
        Func<double> videoAspect,
        Func<SingleFrameResult, PipeCalibration?, IReadOnlyList<SegmentedFinding>> buildSegmentedFindings,
        Func<CancellationToken, Task<byte[]?>> captureSnapshotAsync)
    {
        ArgumentNullException.ThrowIfNull(sessionEvents);
        ArgumentNullException.ThrowIfNull(viewEvents);
        ArgumentNullException.ThrowIfNull(importEvents);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(videoAspect);
        ArgumentNullException.ThrowIfNull(buildSegmentedFindings);
        ArgumentNullException.ThrowIfNull(captureSnapshotAsync);

        _sessionEvents = sessionEvents;
        _viewEvents = viewEvents;
        _importEvents = importEvents;
        _calibration = calibration;
        _videoAspect = videoAspect;
        _buildSegmentedFindings = buildSegmentedFindings;
        _captureSnapshotAsync = captureSnapshotAsync;
    }

    public static CodingAnalysisContext CreateDefault(
        Func<IEnumerable<CodingEvent>?> sessionEvents,
        Func<IEnumerable<CodingEvent>?> viewEvents,
        Func<IEnumerable<CodingEvent>> importEvents,
        Func<PipeCalibration?> calibration,
        Func<double> videoAspect,
        Func<string, bool> takeSnapshot)
        => new(
            sessionEvents,
            viewEvents,
            importEvents,
            calibration,
            videoAspect,
            BuildSegmentedFindingsCore,
            cancellationToken => CodingSnapshotCaptureFactory.CapturePngAsync(takeSnapshot, cancellationToken));

    public bool IsAfterTerminalBoundary(double? currentMeter, TimeSpan currentVideoTime)
        => CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            CodingTerminalBoundaryCandidateBuilder.Enumerate(
                _sessionEvents(),
                _viewEvents(),
                _importEvents()),
            currentMeter,
            currentVideoTime);

    public bool IsFindingTooFarAhead(LiveFrameFinding finding)
        => CodingFindingProximityPolicy.IsTooFarAhead(
            finding,
            _calibration(),
            _videoAspect());

    public IReadOnlyList<SegmentedFinding> BuildSegmentedFindings(SingleFrameResult result)
        => _buildSegmentedFindings(result, _calibration());

    public Task<byte[]?> CaptureSnapshotAsync(CancellationToken cancellationToken)
        => _captureSnapshotAsync(cancellationToken);

    private static IReadOnlyList<SegmentedFinding> BuildSegmentedFindingsCore(
        SingleFrameResult result,
        PipeCalibration? calibration)
        => CodingSegmentedFindingsBuildWorkflow.Execute(
            new CodingSegmentedFindingsBuildRequest(result, calibration),
            new CodingSegmentedFindingsBuildActions(
                BuildSegmentedFindings: (samResponse, dinoDetections, quantifiedMasks, proximityCalibration) =>
                    SegmentedFindingBuilder.Build(
                        samResponse,
                        dinoDetections,
                        quantifiedMasks,
                        proximityCalibration.VanishX,
                        proximityCalibration.VanishY,
                        proximityCalibration.PipeRadiusNorm,
                        MetrierungProximityThresholds.Default)))
            .Segmented;
}
