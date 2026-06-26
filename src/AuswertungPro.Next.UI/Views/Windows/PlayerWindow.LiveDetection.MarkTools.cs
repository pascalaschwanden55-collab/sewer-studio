using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ManualMark_Click(object sender, RoutedEventArgs e)
    {
        _markToolControls.ToggleManualMarkPopup(_codingModeState.IsCodingMode);
    }

    private void ToolsDropdown_Click(object sender, RoutedEventArgs e)
    {
        _markToolControls.ToggleToolsDropdown();
    }

    private void MarkTool_Punkt_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Point, "Punkt");

    private void MarkTool_Ellipse_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Ellipse, "Ellipse");

    private void MarkTool_Freihand_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Freehand, "Freihand");

    private void MarkTool_Rechteck_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Rechteck");

    private void ActivateMarkTool(OverlayToolType tool, string label)
    {
        LiveDetectionManualMarkActivationWorkflow.Execute(
            new LiveDetectionManualMarkActivationWorkflowRequest(tool, label),
            new LiveDetectionManualMarkActivationWorkflowActions(
                BeginActivation: _markToolControls.BeginActivation,
                SetMarkToolType: _liveDetectionController.SetMarkToolType,
                SetPause: _playerPlaybackControlHost.SetPause,
                CancelSchema: _codingSchemaManager.Cancel,
                ClearSchemaType: _codingSchemaTypeState.Clear,
                SetManualMarkMode: _liveDetectionController.SetManualMarkMode,
                ActivatePointTool: _markToolControls.ActivatePointTool,
                EnsureOverlayReady: EnsureMarkOverlayReady,
                SetActiveTool: selectedTool => _codingOverlayToolHost.SetActiveTool(selectedTool),
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                OpenCodingOverlay: _markToolControls.OpenCodingOverlay,
                UpdateCodingOverlayViewport: UpdateCodingOverlayViewport,
                EnableCodingOverlayInput: _markToolControls.EnableCodingOverlayInput));
    }

    /// <summary>
    /// Stellt sicher dass OverlayService + ViewModel bereitstehen (auch ausserhalb Codier-Modus).
    /// </summary>
    private void EnsureMarkOverlayReady()
    {
        LiveDetectionMarkOverlayReadyWorkflow.Execute(
            new LiveDetectionMarkOverlayReadyStateRequest(
                _codingOverlayRuntimeOwner.HasService,
                _codingSessionHost.HasViewModel,
                _playbackContext.VideoPath,
                _dependencies.Settings,
                _codingSessionRuntimeOwner.Service,
                _codingOverlayRuntimeOwner.Service),
            new LiveDetectionMarkOverlayReadyApplyActions(
                SetSessionService: _codingSessionRuntimeOwner.Set,
                SetOverlayService: _codingOverlayRuntimeOwner.Set,
                SetViewModel: viewModel => _codingSessionViewModelOwner.Set(
                    viewModel,
                    observePropertyChanged: false)));
    }

    private void DeactivateMarkTool()
    {
        LiveDetectionManualMarkDeactivationWorkflow.Execute(
            new LiveDetectionManualMarkDeactivationWorkflowRequest(
                _codingModeState.IsCodingMode,
                _liveDetectionController.IsDetecting),
            new LiveDetectionManualMarkDeactivationWorkflowActions(
                SetMarkToolType: _liveDetectionController.SetMarkToolType,
                SetManualMarkMode: _liveDetectionController.SetManualMarkMode,
                ResetToolLabel: _markToolControls.ResetToolLabel,
                DeactivateDetectionSide: _markToolControls.DeactivateDetectionSide,
                CancelSchema: _codingSchemaManager.Cancel,
                CancelDraw: () => _codingOverlayToolHost.CancelDraw(),
                SetActiveTool: tool => _codingOverlayToolHost.SetActiveTool(tool),
                DeactivateCodingOverlay: _markToolControls.DeactivateCodingOverlay));
    }
}
