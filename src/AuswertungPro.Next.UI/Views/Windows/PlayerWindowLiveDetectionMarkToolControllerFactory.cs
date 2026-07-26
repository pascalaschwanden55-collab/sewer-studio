using System;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record PlayerWindowLiveDetectionMarkToolControllerDependencies(
    PlayerMarkToolControls MarkToolControls,
    LiveDetectionController DetectionController,
    PlayerPlaybackControlHost PlaybackControlHost,
    CodingRuntimeStateControllerSet RuntimeStates,
    CodingSchemaStateControllerSet SchemaStates,
    CodingSessionRuntime SessionRuntime,
    Func<string> ResolveVideoPath,
    Func<AppSettings?> ResolveSettings,
    Func<ITrainingSampleStore?> ResolveTrainingSamples,
    Action UpdateCodingOverlayViewport);

internal static class PlayerWindowLiveDetectionMarkToolControllerFactory
{
    internal static ILiveDetectionMarkToolController Create(
        PlayerWindowLiveDetectionMarkToolControllerDependencies dependencies)
    {
        Validate(dependencies);

        return new LiveDetectionMarkToolController(
            new LiveDetectionMarkToolControllerBindings(
                ToggleManualMarkPopup: dependencies.MarkToolControls.ToggleManualMarkPopup,
                ToggleToolsDropdown: dependencies.MarkToolControls.ToggleToolsDropdown,
                CreateActivationActions: ensureOverlayReady =>
                    new LiveDetectionManualMarkActivationWorkflowActions(
                        BeginActivation: dependencies.MarkToolControls.BeginActivation,
                        SetMarkToolType: dependencies.DetectionController.SetMarkToolType,
                        SetPause: dependencies.PlaybackControlHost.SetPause,
                        CancelSchema: dependencies.SchemaStates.OverlayManagerOwner.Cancel,
                        ClearSchemaType: dependencies.SchemaStates.TypeState.Clear,
                        SetManualMarkMode: dependencies.DetectionController.SetManualMarkMode,
                        ActivatePointTool: dependencies.MarkToolControls.ActivatePointTool,
                        EnsureOverlayReady: ensureOverlayReady,
                        SetActiveTool: selectedTool =>
                            dependencies.SessionRuntime.OverlayToolHost.SetActiveTool(selectedTool),
                        ClearCurrentOverlay: dependencies.SessionRuntime.SessionHost.ClearCurrentOverlay,
                        OpenCodingOverlay: dependencies.MarkToolControls.OpenCodingOverlay,
                        UpdateCodingOverlayViewport: dependencies.UpdateCodingOverlayViewport,
                        EnableCodingOverlayInput: dependencies.MarkToolControls.EnableCodingOverlayInput),
                CreateOverlayReadyRequest: () => new LiveDetectionMarkOverlayReadyStateRequest(
                    dependencies.RuntimeStates.OverlayRuntimeOwner.HasService,
                    dependencies.SessionRuntime.SessionHost.HasViewModel,
                    dependencies.ResolveVideoPath(),
                    dependencies.ResolveSettings(),
                    dependencies.RuntimeStates.SessionRuntimeOwner.Service,
                    dependencies.RuntimeStates.OverlayRuntimeOwner.Service,
                    dependencies.ResolveTrainingSamples()),
                OverlayReadyActions: new LiveDetectionMarkOverlayReadyApplyActions(
                    SetSessionService: dependencies.RuntimeStates.SessionRuntimeOwner.Set,
                    SetOverlayService: dependencies.RuntimeStates.OverlayRuntimeOwner.Set,
                    SetViewModel: viewModel => dependencies.SessionRuntime.ViewModelOwner.Set(
                        viewModel,
                        observePropertyChanged: false)),
                CreateDeactivationRequest: () =>
                    new LiveDetectionManualMarkDeactivationWorkflowRequest(
                        dependencies.RuntimeStates.ModeState.IsCodingMode,
                        dependencies.DetectionController.IsDetecting),
                DeactivationActions: new LiveDetectionManualMarkDeactivationWorkflowActions(
                    SetMarkToolType: dependencies.DetectionController.SetMarkToolType,
                    SetManualMarkMode: dependencies.DetectionController.SetManualMarkMode,
                    ResetToolLabel: dependencies.MarkToolControls.ResetToolLabel,
                    DeactivateDetectionSide: dependencies.MarkToolControls.DeactivateDetectionSide,
                    CancelSchema: dependencies.SchemaStates.OverlayManagerOwner.Cancel,
                    CancelDraw: () => dependencies.SessionRuntime.OverlayToolHost.CancelDraw(),
                    SetActiveTool: tool => dependencies.SessionRuntime.OverlayToolHost.SetActiveTool(tool),
                    DeactivateCodingOverlay: dependencies.MarkToolControls.DeactivateCodingOverlay)));
    }

    private static void Validate(
        PlayerWindowLiveDetectionMarkToolControllerDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.MarkToolControls);
        ArgumentNullException.ThrowIfNull(dependencies.DetectionController);
        ArgumentNullException.ThrowIfNull(dependencies.PlaybackControlHost);
        ArgumentNullException.ThrowIfNull(dependencies.RuntimeStates);
        ArgumentNullException.ThrowIfNull(dependencies.SchemaStates);
        ArgumentNullException.ThrowIfNull(dependencies.SessionRuntime);
        ArgumentNullException.ThrowIfNull(dependencies.SessionRuntime.ViewModelOwner);
        ArgumentNullException.ThrowIfNull(dependencies.SessionRuntime.SessionHost);
        ArgumentNullException.ThrowIfNull(dependencies.SessionRuntime.OverlayToolHost);
        ArgumentNullException.ThrowIfNull(dependencies.ResolveVideoPath);
        ArgumentNullException.ThrowIfNull(dependencies.ResolveSettings);
        ArgumentNullException.ThrowIfNull(dependencies.ResolveTrainingSamples);
        ArgumentNullException.ThrowIfNull(dependencies.UpdateCodingOverlayViewport);
    }
}
