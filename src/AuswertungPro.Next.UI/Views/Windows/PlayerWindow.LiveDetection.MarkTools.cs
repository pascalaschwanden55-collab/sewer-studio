using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private OverlayToolType _markToolType = OverlayToolType.None;

    private void ManualMark_Click(object sender, RoutedEventArgs e)
    {
        if (_isCodingMode)
            ToolsDropdownPopup.IsOpen = !ToolsDropdownPopup.IsOpen;
        else
            MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
    }

    private void ToolsDropdown_Click(object sender, RoutedEventArgs e)
    {
        ToolsDropdownPopup.IsOpen = !ToolsDropdownPopup.IsOpen;
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
        MarkToolPopup.IsOpen = false;
        CodingMarkToolPopup.IsOpen = false;
        ToolsDropdownPopup.IsOpen = false;
        _markToolType = tool;
        TxtMarkToolName.Text = label;
        TxtActiveToolLabel.Text = label;
        _player.SetPause(true);
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;

        if (tool == OverlayToolType.Point)
        {
            // Bestehende Punkt-Logik: DetectionCanvas aktivieren
            _isManualMarkMode = true;
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            DetectionOverlayGrid.IsHitTestVisible = true;
            DetectionCanvas.IsHitTestVisible = true;
            DetectionCanvas.Cursor = Cursors.Cross;
        }
        else
        {
            // Zeichen-Tools: CodingOverlayPopup aktivieren
            _isManualMarkMode = false;
            EnsureMarkOverlayReady();
            _codingOverlayService!.ActiveTool = tool;

            // Offene Zeichnung verwerfen
            if (_codingVm != null)
                _codingVm.CurrentOverlay = null;

            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = Cursors.Cross;
        }
    }

    /// <summary>
    /// Stellt sicher dass OverlayService + ViewModel bereitstehen (auch ausserhalb Codier-Modus).
    /// </summary>
    private ICodingSessionService CreateCodingSessionService()
        => CodingSessionServiceFactory.Create(_serviceProvider?.Settings);

    private void EnsureMarkOverlayReady()
    {
        if (_codingOverlayService != null && _codingVm != null) return;

        // Lazy-Init: minimales Setup fuer Overlay-Zeichnung
        _codingOverlayService ??= new OverlayToolService();
        if (_codingVm == null)
        {
            _codingSessionService ??= CreateCodingSessionService();
            _codingVm = new ViewModels.Windows.CodingSessionViewModel(
                _codingSessionService,
                _codingOverlayService,
                new InfraSelfImproving.CodingFeedbackRecorder());
        }
    }

    private void DeactivateMarkTool()
    {
        _markToolType = OverlayToolType.None;
        _isManualMarkMode = false;
        TxtMarkToolName.Text = "Markieren";

        DetectionCanvas.Cursor = Cursors.Arrow;
        DetectionCanvas.IsHitTestVisible = false;
        if (!_isDetecting)
        {
            DetectionOverlayGrid.IsHitTestVisible = false;
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        }

        if (!_isCodingMode)
        {
            _codingSchemaManager.Cancel();
            _codingOverlayService?.CancelDraw();
            if (_codingOverlayService != null)
                _codingOverlayService.ActiveTool = OverlayToolType.None;
            CodingOverlayPopup.IsOpen = false;
            CodingOverlayCanvas.IsHitTestVisible = false;
        }
    }
}
