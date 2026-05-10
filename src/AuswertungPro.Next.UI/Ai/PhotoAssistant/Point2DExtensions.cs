using System.Windows;
using AuswertungPro.Next.Application.Ai.PhotoAssistant;

namespace AuswertungPro.Next.UI.Ai.PhotoAssistant;

/// <summary>
/// Konvertierung zwischen WPF-<see cref="Point"/> und WPF-frei
/// <see cref="Point2D"/>.
///
/// Phase 1.5b (2026-05-10): die PhotoAssistant-Math-Services sind nach
/// Application/Ai/PhotoAssistant migriert und arbeiten mit Point2D.
/// UI-Caller (PhotoMeasurementWindow.PhotoAssistant) konvertieren hier.
/// </summary>
public static class Point2DExtensions
{
    public static Point2D ToPoint2D(this Point p) => new(p.X, p.Y);

    public static Point ToWpfPoint(this Point2D p) => new(p.X, p.Y);
}
