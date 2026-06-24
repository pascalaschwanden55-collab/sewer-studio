using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerMarkToolControls
{
    private readonly Popup _markToolPopup;
    private readonly Popup _codingMarkToolPopup;
    private readonly Popup _toolsDropdownPopup;
    private readonly TextBlock _markToolName;
    private readonly TextBlock _activeToolLabel;
    private readonly UIElement _detectionOverlayGrid;
    private readonly Canvas _detectionCanvas;
    private readonly Popup _codingOverlayPopup;
    private readonly Canvas _codingOverlayCanvas;

    public PlayerMarkToolControls(
        Popup markToolPopup,
        Popup codingMarkToolPopup,
        Popup toolsDropdownPopup,
        TextBlock markToolName,
        TextBlock activeToolLabel,
        UIElement detectionOverlayGrid,
        Canvas detectionCanvas,
        Popup codingOverlayPopup,
        Canvas codingOverlayCanvas)
    {
        _markToolPopup = markToolPopup;
        _codingMarkToolPopup = codingMarkToolPopup;
        _toolsDropdownPopup = toolsDropdownPopup;
        _markToolName = markToolName;
        _activeToolLabel = activeToolLabel;
        _detectionOverlayGrid = detectionOverlayGrid;
        _detectionCanvas = detectionCanvas;
        _codingOverlayPopup = codingOverlayPopup;
        _codingOverlayCanvas = codingOverlayCanvas;
    }

    public void ToggleManualMarkPopup(bool isCodingMode)
    {
        if (isCodingMode)
            ToggleToolsDropdown();
        else
            _markToolPopup.IsOpen = !_markToolPopup.IsOpen;
    }

    public void ToggleToolsDropdown()
    {
        _toolsDropdownPopup.IsOpen = !_toolsDropdownPopup.IsOpen;
    }

    public void BeginActivation(string label)
    {
        _markToolPopup.IsOpen = false;
        _codingMarkToolPopup.IsOpen = false;
        _toolsDropdownPopup.IsOpen = false;
        SetToolLabels(label);
    }

    public void SetToolLabels(string label)
    {
        _markToolName.Text = label;
        _activeToolLabel.Text = label;
    }

    public void ActivatePointTool()
    {
        _detectionOverlayGrid.Visibility = Visibility.Visible;
        _detectionOverlayGrid.IsHitTestVisible = true;
        _detectionCanvas.IsHitTestVisible = true;
        _detectionCanvas.Cursor = Cursors.Cross;
    }

    public void OpenCodingOverlay()
    {
        _codingOverlayPopup.IsOpen = true;
    }

    public void EnableCodingOverlayInput()
    {
        _codingOverlayCanvas.IsHitTestVisible = true;
        _codingOverlayCanvas.Cursor = Cursors.Cross;
    }

    public void ResetToolLabel()
    {
        _markToolName.Text = "Markieren";
    }

    public void DeactivateDetectionSide(bool isDetecting)
    {
        _detectionCanvas.Cursor = Cursors.Arrow;
        _detectionCanvas.IsHitTestVisible = false;

        if (isDetecting)
            return;

        _detectionOverlayGrid.IsHitTestVisible = false;
        _detectionOverlayGrid.Visibility = Visibility.Collapsed;
    }

    public void DeactivateCodingOverlay()
    {
        _codingOverlayPopup.IsOpen = false;
        _codingOverlayCanvas.IsHitTestVisible = false;
    }
}
