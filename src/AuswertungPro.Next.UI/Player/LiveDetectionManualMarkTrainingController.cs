using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record LiveDetectionManualMarkTrainingControllerActions(
    Func<OverlayGeometry, double, ProtocolEntry?> SelectEntry,
    Func<string?> ResolveDisplayedMeterText,
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Func<Task<byte[]?>> CaptureCurrentFrameAsync,
    Action RefreshCodingEvents,
    Action<string, bool> ShowOsdMeterStatus);

public sealed class LiveDetectionManualMarkTrainingController
{
    private readonly ILiveDetectionTrainingAnnotationWriter _annotationWriter;
    private readonly LiveDetectionManualMarkTrainingControllerActions _actions;

    public LiveDetectionManualMarkTrainingController(
        ILiveDetectionTrainingAnnotationWriter annotationWriter,
        LiveDetectionManualMarkTrainingControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(annotationWriter);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.SelectEntry);
        ArgumentNullException.ThrowIfNull(actions.ResolveDisplayedMeterText);
        ArgumentNullException.ThrowIfNull(actions.ResolveCodingSessionService);
        ArgumentNullException.ThrowIfNull(actions.CaptureCurrentFrameAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshCodingEvents);
        ArgumentNullException.ThrowIfNull(actions.ShowOsdMeterStatus);

        _annotationWriter = annotationWriter;
        _actions = actions;
    }

    public Task<LiveDetectionManualMarkTrainingCommandResult> SaveAsync(
        OverlayGeometry overlay,
        double timestampSeconds,
        string? clockPosition,
        byte[]? preCapturedFrame = null)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        return LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync(
            new LiveDetectionManualMarkTrainingCommandActions(
                SelectEntry: () => _actions.SelectEntry(overlay, timestampSeconds),
                SaveTrainingAsync: selectedEntry => SaveTrainingAsync(
                    selectedEntry,
                    overlay,
                    timestampSeconds,
                    clockPosition,
                    preCapturedFrame),
                HandleTrainingResult: trainingResult =>
                    LiveDetectionManualMarkTrainingResultWorkflow.Execute(
                        trainingResult,
                        new LiveDetectionManualMarkTrainingResultActions(
                            _actions.ShowOsdMeterStatus)),
                ShowOsdMeterStatus: _actions.ShowOsdMeterStatus));
    }

    private Task<LiveDetectionManualMarkTrainingResult> SaveTrainingAsync(
        ProtocolEntry selectedEntry,
        OverlayGeometry overlay,
        double timestampSeconds,
        string? clockPosition,
        byte[]? preCapturedFrame)
        => LiveDetectionManualMarkTrainingWorkflow.SaveAsync(
            selectedEntry,
            overlay,
            timestampSeconds,
            clockPosition,
            _actions.ResolveDisplayedMeterText(),
            _actions.ResolveCodingSessionService(),
            preCapturedFrame,
            _actions.CaptureCurrentFrameAsync,
            (frameBytes, entry, markOverlay, clock, meter, videoTimestamp) =>
                _annotationWriter.SaveManualMarkAsync(
                    frameBytes,
                    entry,
                    markOverlay,
                    clock,
                    meter,
                    videoTimestamp),
            _actions.RefreshCodingEvents);
}
