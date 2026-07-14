using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Player;

public sealed record LiveDetectionTrainingControllerSet(
    LiveDetectionConfirmationTrainingController Confirmation,
    LiveDetectionManualMarkTrainingController ManualMark);

public sealed record LiveDetectionTrainingControllerSetDependencies(
    LiveDetectionController DetectionController,
    PlayerTimelineHost TimelineHost,
    Window Owner,
    string? VideoPath,
    Func<double?> ResolveAutomaticMeter,
    Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> CreateCorrectionViewModel,
    Func<CodingCodeExplorerSeedSelectionWorkflowActions> CreateManualSelectionActions,
    Func<string?> ResolveDisplayedMeterText,
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Func<Task<byte[]?>> CaptureCurrentFrameAsync,
    Action RefreshCodingEvents,
    Action<string, bool> ShowOsdMeterStatus,
    Action ResumeDetection);

public static class LiveDetectionTrainingControllerSetFactory
{
    public static LiveDetectionTrainingControllerSet Create(
        LiveDetectionTrainingControllerSetDependencies dependencies,
        ILiveDetectionTrainingAnnotationWriter? annotationWriter = null)
    {
        Validate(dependencies);
        annotationWriter ??= LiveDetectionTrainingAnnotationWriter.CreateDefault();

        var confirmation = new LiveDetectionConfirmationTrainingController(
            dependencies.DetectionController,
            dependencies.TimelineHost,
            annotationWriter,
            new LiveDetectionConfirmationTrainingControllerActions(
                ResolveAutomaticMeter: dependencies.ResolveAutomaticMeter,
                SelectCorrection: (meter, timestampSeconds) =>
                    LiveDetectionCorrectionCodeSelectionWorkflow.Select(
                        new LiveDetectionCorrectionCodeSelectionRequest(
                            meter,
                            timestampSeconds,
                            dependencies.VideoPath,
                            dependencies.Owner),
                        new LiveDetectionCorrectionCodeSelectionActions(
                            dependencies.CreateCorrectionViewModel)),
                CaptureCurrentFrameAsync: dependencies.CaptureCurrentFrameAsync,
                ShowOsdMeterStatus: dependencies.ShowOsdMeterStatus,
                ResumeDetection: dependencies.ResumeDetection));

        var manualMark = new LiveDetectionManualMarkTrainingController(
            annotationWriter,
            new LiveDetectionManualMarkTrainingControllerActions(
                SelectEntry: (overlay, timestampSeconds) =>
                    CodingCodeExplorerSeedSelectionWorkflow.Execute(
                        new CodingCodeExplorerSeedSelectionWorkflowRequest(
                            overlay,
                            dependencies.ResolveAutomaticMeter(),
                            TimeSpan.FromSeconds(timestampSeconds),
                            dependencies.VideoPath,
                            dependencies.Owner),
                        dependencies.CreateManualSelectionActions()),
                ResolveDisplayedMeterText: dependencies.ResolveDisplayedMeterText,
                ResolveCodingSessionService: dependencies.ResolveCodingSessionService,
                CaptureCurrentFrameAsync: dependencies.CaptureCurrentFrameAsync,
                RefreshCodingEvents: dependencies.RefreshCodingEvents,
                ShowOsdMeterStatus: dependencies.ShowOsdMeterStatus));

        return new LiveDetectionTrainingControllerSet(confirmation, manualMark);
    }

    private static void Validate(LiveDetectionTrainingControllerSetDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.DetectionController);
        ArgumentNullException.ThrowIfNull(dependencies.TimelineHost);
        ArgumentNullException.ThrowIfNull(dependencies.Owner);
        ArgumentNullException.ThrowIfNull(dependencies.ResolveAutomaticMeter);
        ArgumentNullException.ThrowIfNull(dependencies.CreateCorrectionViewModel);
        ArgumentNullException.ThrowIfNull(dependencies.CreateManualSelectionActions);
        ArgumentNullException.ThrowIfNull(dependencies.ResolveDisplayedMeterText);
        ArgumentNullException.ThrowIfNull(dependencies.ResolveCodingSessionService);
        ArgumentNullException.ThrowIfNull(dependencies.CaptureCurrentFrameAsync);
        ArgumentNullException.ThrowIfNull(dependencies.RefreshCodingEvents);
        ArgumentNullException.ThrowIfNull(dependencies.ShowOsdMeterStatus);
        ArgumentNullException.ThrowIfNull(dependencies.ResumeDetection);
    }
}
