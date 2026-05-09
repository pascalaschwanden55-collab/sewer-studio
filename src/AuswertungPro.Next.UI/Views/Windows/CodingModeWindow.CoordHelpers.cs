using System.Windows;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow Pixel/Norm-Umrechnungshelfer (Slice 8a.2.7): zwei
// reine Koordinaten-Konverter (Pixel ↔ NormalizedPoint anhand der
// OverlayCanvas-Groesse). Keine State- oder Workflow-Aenderung. Aus
// dem Hauptdatei extrahiert. Calibration-Methoden BtnCalibrate_*,
// ApplyCalibration und RenderPreview bleiben im Hauptdatei — sie ziehen
// Workflow-/Session-State und brauchen ggf. eine eigene ADR.
public partial class CodingModeWindow
{
    private NormalizedPoint PixelToNormalized(Point pixel)
    {
        double w = OverlayCanvas.ActualWidth;
        double h = OverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return new NormalizedPoint(0.5, 0.5);
        return new NormalizedPoint(pixel.X / w, pixel.Y / h);
    }

    private Point NormalizedToPixel(NormalizedPoint normalized)
    {
        return new Point(
            normalized.X * OverlayCanvas.ActualWidth,
            normalized.Y * OverlayCanvas.ActualHeight);
    }
}
