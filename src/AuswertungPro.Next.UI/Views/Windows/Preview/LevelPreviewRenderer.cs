using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows.Preview;

// Vorschau fuer das Level-Werkzeug: horizontale Linie als Pegelhoehe
// + halbtransparente Fuellflaeche, ueber den Rohrkreis (Calibration)
// als EllipseGeometry geclippt. Drei Sub-Modi steuern Farbe und
// Fuellrichtung:
//
//   Water      → blau,    Linie nach UNTEN gefuellt (bis Rohrunterseite)
//   Obstacle   → rot,     Linie nach OBEN gefuellt (bis Rohroberseite)
//   Sediment   → braun,   gleich wie Water (default-Pfad)
//
// Wenn _vm.CurrentOverlay bereits ein Level ist, wird zusaetzlich ein
// Mode+FillPercent-Label oben gezeichnet.
internal sealed class LevelPreviewRenderer : IPreviewToolRenderer
{
    public void Render(PreviewRenderContext ctx)
    {
        var p1 = ctx.ToPixel(ctx.Start);
        var p2 = ctx.ToPixel(ctx.Current);

        var levelStroke = ctx.OverlayService.ActiveLevelMode switch
        {
            LevelMode.Water => Brushes.RoyalBlue,
            LevelMode.Obstacle => Brushes.Crimson,
            _ => Brushes.Chocolate
        };
        double levelY = p2.Y;

        // Fuellflaeche mit Rohr-Ellipsen-Clip (nicht ueber den Kreis hinaus)
        var calib = ctx.OverlayService.Calibration;
        var pipeCenter = calib?.PipeCenter ?? new NormalizedPoint(0.5, 0.5);
        double pipeR = (calib?.NormalizedDiameter ?? 0.7) / 2.0;
        var pipeCenterPx = ctx.ToPixel(pipeCenter);
        double rxPx = pipeR * ctx.Canvas.ActualWidth;
        double ryPx = pipeR * ctx.Canvas.ActualHeight;

        bool isObstacle = ctx.OverlayService.ActiveLevelMode == LevelMode.Obstacle;
        double fillTop    = isObstacle ? (pipeCenterPx.Y - ryPx) : levelY;
        double fillBottom = isObstacle ? levelY : (pipeCenterPx.Y + ryPx);

        var strokeColor = ((SolidColorBrush)levelStroke).Color;
        var fillRect = new Rectangle
        {
            Width = rxPx * 2,
            Height = Math.Abs(fillBottom - fillTop),
            Fill = new SolidColorBrush(Color.FromArgb(38, strokeColor.R, strokeColor.G, strokeColor.B)),
            Tag = ctx.PreviewTag,
            Clip = new EllipseGeometry(
                new Point(rxPx, pipeCenterPx.Y - Math.Min(fillTop, fillBottom)),
                rxPx, ryPx)
        };
        Canvas.SetLeft(fillRect, pipeCenterPx.X - rxPx);
        Canvas.SetTop(fillRect, Math.Min(fillTop, fillBottom));
        ctx.Canvas.Children.Add(fillRect);

        // Pegellinie auf gleicher Y-Hoehe quer durch
        ctx.Canvas.Children.Add(new Line
        {
            X1 = p1.X, Y1 = levelY,
            X2 = p2.X, Y2 = levelY,
            Stroke = levelStroke,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Tag = ctx.PreviewTag
        });

        // Mode + FillPercent-Label nur waehrend einer aktiven Level-Geometrie
        if (ctx.CurrentOverlay?.ToolType == OverlayToolType.Level)
        {
            var modeText = ctx.OverlayService.ActiveLevelMode switch
            {
                LevelMode.Water => "Wasser",
                LevelMode.Obstacle => "Hindernis",
                _ => "Ablagerung"
            };
            var text = ctx.CurrentOverlay.FillPercent.HasValue
                ? $"{modeText}: {ctx.CurrentOverlay.FillPercent:F1}%"
                : $"{modeText}: ...";
            var label = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(4, 2, 4, 2),
                Tag = ctx.PreviewTag
            };
            Canvas.SetLeft(label, (p1.X + p2.X) / 2 + 6);
            Canvas.SetTop(label, levelY - 16);
            ctx.Canvas.Children.Add(label);
        }
    }
}
