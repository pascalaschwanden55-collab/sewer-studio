using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows.Preview;

// Vorschau fuer das Rectangle-Werkzeug: cyan-gestricheltes Rechteck mit
// halbtransparentem Cyan-Fill, von Start- zu Current-Punkt aufgespannt.
// Pilot fuer das Strategy-Pattern (Slice 8a.2.11 Step 2).
internal sealed class RectanglePreviewRenderer : IPreviewToolRenderer
{
    public OverlayToolType ToolType => OverlayToolType.Rectangle;

    public void Render(PreviewRenderContext ctx)
    {
        var p1 = ctx.ToPixel(ctx.Start);
        var p2 = ctx.ToPixel(ctx.Current);

        var rect = new Rectangle
        {
            Width = Math.Abs(p2.X - p1.X),
            Height = Math.Abs(p2.Y - p1.Y),
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 255)),
            Tag = ctx.PreviewTag
        };
        Canvas.SetLeft(rect, Math.Min(p1.X, p2.X));
        Canvas.SetTop(rect, Math.Min(p1.Y, p2.Y));
        ctx.Canvas.Children.Add(rect);
    }
}
