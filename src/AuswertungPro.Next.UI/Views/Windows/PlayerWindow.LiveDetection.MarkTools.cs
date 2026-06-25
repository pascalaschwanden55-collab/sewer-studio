using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ManualMark_Click(object sender, RoutedEventArgs e)
    {
        _markToolControls.ToggleManualMarkPopup(_isCodingMode);
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
                SetMarkToolType: selectedTool => _markToolType = selectedTool,
                SetPause: _playerPlaybackControlHost.SetPause,
                CancelSchema: _codingSchemaManager.Cancel,
                ClearSchemaType: () => _codingSchemaType = null,
                SetManualMarkMode: enabled => _isManualMarkMode = enabled,
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
        if (_codingOverlayRuntimeOwner.HasService && _codingSessionHost.HasViewModel) return;

        var state = CodingSessionStateFactory.Create(
            _videoPath,
            _dependencies.Settings,
            _codingSessionRuntimeOwner.Service,
            _codingOverlayRuntimeOwner.Service);
        _codingSessionRuntimeOwner.Set(state.SessionService);
        _codingOverlayRuntimeOwner.Set(state.OverlayService);
        _codingSessionViewModelOwner.Set(state.ViewModel, observePropertyChanged: false);
    }

    private void DeactivateMarkTool()
    {
        LiveDetectionManualMarkDeactivationWorkflow.Execute(
            new LiveDetectionManualMarkDeactivationWorkflowRequest(
                _isCodingMode,
                _liveDetectionController.IsDetecting),
            new LiveDetectionManualMarkDeactivationWorkflowActions(
                SetMarkToolType: tool => _markToolType = tool,
                SetManualMarkMode: enabled => _isManualMarkMode = enabled,
                ResetToolLabel: _markToolControls.ResetToolLabel,
                DeactivateDetectionSide: _markToolControls.DeactivateDetectionSide,
                CancelSchema: _codingSchemaManager.Cancel,
                CancelDraw: () => _codingOverlayToolHost.CancelDraw(),
                SetActiveTool: tool => _codingOverlayToolHost.SetActiveTool(tool),
                DeactivateCodingOverlay: _markToolControls.DeactivateCodingOverlay));
    }
}
