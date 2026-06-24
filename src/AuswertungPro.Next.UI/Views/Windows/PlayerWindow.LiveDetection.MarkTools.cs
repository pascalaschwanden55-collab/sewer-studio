using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;
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
        _markToolControls.BeginActivation(label);
        _markToolType = tool;
        PlayerManualMarkPlayback.PauseForManualMarking(pause => _player.SetPause(pause));
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;

        if (tool == OverlayToolType.Point)
        {
            // Bestehende Punkt-Logik: DetectionCanvas aktivieren
            _isManualMarkMode = true;
            _markToolControls.ActivatePointTool();
        }
        else
        {
            // Zeichen-Tools: CodingOverlayPopup aktivieren
            _isManualMarkMode = false;
            EnsureMarkOverlayReady();
            _codingOverlayService!.ActiveTool = tool;

            // Offene Zeichnung verwerfen
            _codingSessionHost.ClearCurrentOverlay();

            _markToolControls.OpenCodingOverlay();
            UpdateCodingOverlayViewport();
            _markToolControls.EnableCodingOverlayInput();
        }
    }

    /// <summary>
    /// Stellt sicher dass OverlayService + ViewModel bereitstehen (auch ausserhalb Codier-Modus).
    /// </summary>
    private void EnsureMarkOverlayReady()
    {
        if (_codingOverlayService != null && _codingSessionHost.HasViewModel) return;

        var state = CodingSessionStateFactory.Create(
            _videoPath,
            _dependencies.Settings,
            _codingSessionService,
            _codingOverlayService);
        _codingSessionService = state.SessionService;
        _codingOverlayService = state.OverlayService;
        _codingVm = state.ViewModel;
    }

    private void DeactivateMarkTool()
    {
        _markToolType = OverlayToolType.None;
        _isManualMarkMode = false;
        _markToolControls.ResetToolLabel();
        _markToolControls.DeactivateDetectionSide(_liveDetectionController.IsDetecting);

        if (!_isCodingMode)
        {
            _codingSchemaManager.Cancel();
            _codingOverlayService?.CancelDraw();
            if (_codingOverlayService != null)
                _codingOverlayService.ActiveTool = OverlayToolType.None;
            _markToolControls.DeactivateCodingOverlay();
        }
    }
}
