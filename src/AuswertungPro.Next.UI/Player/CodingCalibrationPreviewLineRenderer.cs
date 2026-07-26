using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public static class CodingCalibrationPreviewLineRenderer
{
    public static Line Render(Canvas canvas, CodingCalibrationPreviewState preview)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var line = new Line
        {
            X1 = preview.Start.X,
            Y1 = preview.Start.Y,
            X2 = preview.End.X,
            Y2 = preview.End.Y,
            Stroke = Brushes.Magenta,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Tag = OverlayTags.Preview
        };

        canvas.Children.Add(line);
        return line;
    }
}
