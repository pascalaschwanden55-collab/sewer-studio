using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public static class ReferenceDnOverlayRenderer
{
    public static bool Render(
        Canvas canvas,
        PipeCalibration? calibration,
        bool showReferenceDn,
        double canvasWidth,
        double canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var old = canvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s == OverlayTags.RefDn)
            .ToList();
        foreach (var element in old)
            canvas.Children.Remove(element);

        if (!showReferenceDn ||
            calibration is null ||
            !calibration.IsCalibrated ||
            calibration.NormalizedDiameter <= 0 ||
            canvasWidth <= 0 ||
            canvasHeight <= 0)
            return false;

        var circleRect = ReferenceDnGeometry.BuildCircleRect(
            calibration.PipeCenter,
            calibration.NormalizedDiameter,
            canvasWidth,
            canvasHeight);
        if (circleRect.IsEmpty)
            return false;

        var circle = new System.Windows.Shapes.Ellipse
        {
            Width = circleRect.Width,
            Height = circleRect.Height,
            Stroke = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Tag = OverlayTags.RefDn
        };
        Canvas.SetLeft(circle, circleRect.Left);
        Canvas.SetTop(circle, circleRect.Top);
        canvas.Children.Add(circle);

        var label = new TextBlock
        {
            Text = $"Ref: DN {calibration.NominalDiameterMm}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            Tag = OverlayTags.RefDn
        };
        Canvas.SetLeft(label, circleRect.Right + 4);
        Canvas.SetTop(label, circleRect.Top + circleRect.Height / 2.0 - 8);
        canvas.Children.Add(label);

        return true;
    }
}
