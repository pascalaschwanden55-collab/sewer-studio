using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Player;

public sealed record LiveDetectionConfirmationTrainingControllerActions(
    Func<double?> ResolveAutomaticMeter,
    Func<double?, double, ProtocolEntry?> SelectCorrection,
    Func<Task<byte[]?>> CaptureCurrentFrameAsync,
    Action<string, bool> ShowOsdMeterStatus,
    Action ResumeDetection);

public sealed class LiveDetectionConfirmationTrainingController
{
    private readonly LiveDetectionController _detectionController;
    private readonly PlayerTimelineHost _timelineHost;
    private readonly ILiveDetectionTrainingAnnotationWriter _annotationWriter;
    private readonly LiveDetectionConfirmationTrainingControllerActions _actions;

    public LiveDetectionConfirmationTrainingController(
        LiveDetectionController detectionController,
        PlayerTimelineHost timelineHost,
        ILiveDetectionTrainingAnnotationWriter annotationWriter,
        LiveDetectionConfirmationTrainingControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(detectionController);
        ArgumentNullException.ThrowIfNull(timelineHost);
        ArgumentNullException.ThrowIfNull(annotationWriter);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ResolveAutomaticMeter);
        ArgumentNullException.ThrowIfNull(actions.SelectCorrection);
        ArgumentNullException.ThrowIfNull(actions.CaptureCurrentFrameAsync);
        ArgumentNullException.ThrowIfNull(actions.ShowOsdMeterStatus);
        ArgumentNullException.ThrowIfNull(actions.ResumeDetection);

        _detectionController = detectionController;
        _timelineHost = timelineHost;
        _annotationWriter = annotationWriter;
        _actions = actions;
    }

    public Task<LiveDetectionConfirmationAcceptCommandResult> AcceptAsync()
    {
        var pendingFindings = _detectionController.PendingConfirmationFindings;
        return LiveDetectionConfirmationAcceptCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationAcceptCommandRequest(pendingFindings.Count > 0),
            new LiveDetectionConfirmationAcceptCommandActions(
                SaveAcceptedAsync: () => SaveAcceptedAsync(pendingFindings),
                HandleAcceptedResult: result => LiveDetectionConfirmationTrainingResultWorkflow.ExecuteAccepted(
                    result,
                    ResultActions()),
                ShowOsdMeterStatus: _actions.ShowOsdMeterStatus,
                ResumeDetection: _actions.ResumeDetection));
    }

    public Task<LiveDetectionConfirmationCorrectCommandResult> CorrectAsync()
    {
        var pendingFindings = _detectionController.PendingConfirmationFindings;
        var currentTimestampSeconds = _timelineHost.CurrentSecondsOrZero;

        return LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationCorrectCommandRequest(pendingFindings.Count > 0),
            new LiveDetectionConfirmationCorrectCommandActions(
                SelectCorrection: () => _actions.SelectCorrection(
                    _actions.ResolveAutomaticMeter(),
                    currentTimestampSeconds),
                SaveCorrectedAsync: selectedEntry => SaveCorrectedAsync(
                    pendingFindings,
                    selectedEntry,
                    currentTimestampSeconds),
                HandleCorrectedResult: result => LiveDetectionConfirmationTrainingResultWorkflow.ExecuteCorrected(
                    result,
                    ResultActions()),
                ShowOsdMeterStatus: _actions.ShowOsdMeterStatus,
                ResumeDetection: _actions.ResumeDetection));
    }

    private Task<LiveDetectionConfirmationTrainingResult> SaveAcceptedAsync(
        IReadOnlyList<LiveFrameFinding> pendingFindings)
    {
        var timestampSeconds = _detectionController.PendingConfirmationTimestampSeconds
            ?? _timelineHost.CurrentSecondsOrZero;
        return LiveDetectionConfirmationTrainingWorkflow.SaveAcceptedAsync(
            pendingFindings,
            timestampSeconds,
            _detectionController.PendingConfirmationFrameBytes,
            _actions.CaptureCurrentFrameAsync,
            (frameBytes, finding, videoTimestamp) =>
                _annotationWriter.SaveAcceptedAsync(frameBytes, finding, videoTimestamp));
    }

    private Task<LiveDetectionConfirmationTrainingResult> SaveCorrectedAsync(
        IReadOnlyList<LiveFrameFinding> pendingFindings,
        ProtocolEntry selectedEntry,
        double currentTimestampSeconds)
    {
        var timestampSeconds = _detectionController.PendingConfirmationTimestampSeconds
            ?? currentTimestampSeconds;
        return LiveDetectionConfirmationTrainingWorkflow.SaveCorrectedAsync(
            pendingFindings,
            selectedEntry,
            timestampSeconds,
            _detectionController.PendingConfirmationFrameBytes,
            _actions.CaptureCurrentFrameAsync,
            (frameBytes, finding, entry, videoTimestamp) =>
                _annotationWriter.SaveCorrectedAsync(frameBytes, finding, entry, videoTimestamp));
    }

    private LiveDetectionConfirmationTrainingResultActions ResultActions()
        => new(
            ShowOsdMeterStatus: _actions.ShowOsdMeterStatus,
            ResumeDetection: _actions.ResumeDetection);
}
