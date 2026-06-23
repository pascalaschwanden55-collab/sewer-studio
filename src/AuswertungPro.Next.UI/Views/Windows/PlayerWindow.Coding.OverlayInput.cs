using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- Coding Canvas-Events ---

    private void CodingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker hat Vorrang: Rechteck ziehen
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing)
        {
            EingabemarkerCanvas_MouseDown(e.GetPosition(CodingOverlayCanvas));
            e.Handled = true;
            return;
        }
        // Input-Phase: Canvas-Klicks ignorieren (ComboBox ist aktiv)
        if (_eingabemarkerPhase == EingabemarkerPhase.Input ||
            _eingabemarkerPhase == EingabemarkerPhase.Analyzing)
        {
            e.Handled = true;
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (TryStartCodingCalibration(norm))
            return;

        if (_codingOverlayService.ActiveTool == OverlayToolType.None) return;

        if (TryHandleCodingSchemaMouseDown(norm))
            return;

        if (_codingOverlayService.IsMultiPointTool)
        {
            HandleCodingMultiPointMouseDown(norm);
            return; // Kein CaptureMouse bei Multi-Punkt
        }

        HandleCodingStandardMouseDown(norm);
    }

    private void CodingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // Eingabemarker Rechteck-Drag
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing && _eingabemarkerPreviewRect != null)
        {
            EingabemarkerCanvas_MouseMove(e.GetPosition(CodingOverlayCanvas));
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (TryPreviewCodingCalibration(norm))
            return;

        if (TryHandleCodingSchemaMouseMove(norm))
            return;

        if (TryHandleCodingMultiPointMouseMove(norm))
            return;

        TryHandleCodingStandardMouseMove(norm);
    }

    private void CodingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker Rechteck fertig
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing)
        {
            EingabemarkerCanvas_MouseUp(e.GetPosition(CodingOverlayCanvas));
            e.Handled = true;
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (TryFinishCodingCalibration(norm))
            return;

        if (TryHandleCodingSchemaMouseUp(norm))
            return;

        TryHandleCodingStandardMouseUp(norm);
    }
}
