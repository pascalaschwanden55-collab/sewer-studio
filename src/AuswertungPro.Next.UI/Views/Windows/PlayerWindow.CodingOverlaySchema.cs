using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

// PlayerWindow.CodingOverlay Schema-Rendering: Zeichnet die fachlichen
// Schema-Layer (Pipe-Reference + AddSchemaLabel + RenderActiveCodingSchema
// fuer Anschluss/Bogen/Knick/Wasserstand/Querschnitts-Reduktion). Aus dem
// Hauptdatei extrahiert (Slice 26a).
public partial class PlayerWindow
{
    private void RenderActiveCodingSchema()
    {
        if (!_codingSchemaManager.IsActive || _codingSchemaManager.Active == null)
            return;

        var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 8,
            ShadowDepth = 0,
            Opacity = 0.95
        };

        switch (_codingSchemaManager.Active)
        {
            case PipeBendSchema bend:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay != null)
                    RenderPipeBendOverlay(overlay, true, Brushes.Gold, glowEffect, "overlay_preview", bend.Center);

                var center = CodingNormToPixel(bend.Center);
                var radiusHandle = CodingNormToPixel(bend.GetRadiusHandle());

                var guide = new System.Windows.Shapes.Line
                {
                    X1 = center.X,
                    Y1 = center.Y,
                    X2 = radiusHandle.X,
                    Y2 = radiusHandle.Y,
                    Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 184, 0)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(guide);

                AddDotMarker(radiusHandle, 5, Brushes.White, "overlay_preview", glowEffect);
                break;
            }

            case FillLevelSchema fill:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay == null || overlay.Points.Count < 2)
                    return;

                var strokeColor = fill.Mode switch
                {
                    LevelMode.Water => Color.FromRgb(65, 105, 225),
                    LevelMode.Obstacle => Color.FromRgb(220, 20, 60),
                    _ => Color.FromRgb(210, 105, 30)
                };
                var stroke = new SolidColorBrush(strokeColor);
                var fillBrush = new SolidColorBrush(Color.FromArgb(68, strokeColor.R, strokeColor.G, strokeColor.B));

                RenderSchemaPipeReference(fill.PipeCenter, fill.PipeRadius, stroke, glowEffect, "overlay_preview");

                var center = CodingNormToPixel(fill.PipeCenter);
                double rPx = fill.PipeRadius * Math.Min(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight);
                double rx = rPx;
                double ry = rPx;
                double top = center.Y - rPx;
                double bottom = center.Y + rPx;
                var lineP1 = CodingNormToPixel(overlay.Points[0]);
                var lineP2 = CodingNormToPixel(overlay.Points[1]);
                double levelY = lineP1.Y;

                var segment = new Rectangle
                {
                    Width = Math.Max(1, rx * 2),
                    Height = Math.Max(1, fill.Mode == LevelMode.Obstacle ? levelY - top : bottom - levelY),
                    Fill = fillBrush,
                    Tag = "overlay_preview",
                    Clip = new EllipseGeometry(center, rx, ry)
                };
                Canvas.SetLeft(segment, center.X - rx);
                Canvas.SetTop(segment, fill.Mode == LevelMode.Obstacle ? top : levelY);
                CodingOverlayCanvas.Children.Add(segment);

                var levelLine = new System.Windows.Shapes.Line
                {
                    X1 = lineP1.X,
                    Y1 = levelY,
                    X2 = lineP2.X,
                    Y2 = levelY,
                    Stroke = stroke,
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Effect = glowEffect,
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(levelLine);

                AddDotMarker(new Point(center.X, levelY), 6, stroke, "overlay_preview", glowEffect);
                AddSchemaLabel(new Point(center.X, levelY), $"{overlay.FillPercent:F1}%", stroke, glowEffect);
                break;
            }

            case IntrusionSchema intrusion:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay == null)
                    return;

                var stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                var fillBrush = new SolidColorBrush(Color.FromArgb(72, 239, 68, 68));

                RenderSchemaPipeReference(intrusion.PipeCenter, intrusion.PipeRadius, stroke, glowEffect, "overlay_preview");

                var tip = CodingNormToPixel(intrusion.GetIntrusionTip());
                var edge = CodingNormToPixel(intrusion.GetEdgePoint());
                var (leftNorm, rightNorm) = intrusion.GetSpreadEdges();
                var left = CodingNormToPixel(leftNorm);
                var right = CodingNormToPixel(rightNorm);

                var tongue = new System.Windows.Shapes.Polygon
                {
                    Stroke = stroke,
                    StrokeThickness = 2.5,
                    Fill = fillBrush,
                    Effect = glowEffect,
                    Tag = "overlay_preview"
                };
                tongue.Points.Add(left);
                tongue.Points.Add(tip);
                tongue.Points.Add(right);
                CodingOverlayCanvas.Children.Add(tongue);

                var spine = new System.Windows.Shapes.Line
                {
                    X1 = edge.X,
                    Y1 = edge.Y,
                    X2 = tip.X,
                    Y2 = tip.Y,
                    Stroke = stroke,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Effect = glowEffect,
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(spine);

                AddDotMarker(tip, 7, stroke, "overlay_preview", glowEffect);
                AddDotMarker(edge, 5, Brushes.White, "overlay_preview", glowEffect);
                AddSchemaLabel(tip, $"{overlay.FillPercent:F1}% @ {overlay.ClockFrom:F1}h", stroke, glowEffect);
                break;
            }

            case PipeDirectionSchema pipeDir:
            {
                var pipeDirectionOverlay = BuildCodingSchemaGeometry();
                if (pipeDirectionOverlay != null)
                    RenderPipeDirectionOverlay(pipeDirectionOverlay, true, glowEffect, "overlay_preview");

                // Zwei Ellipsen + Verbindungslinie + Winkel-Label
                var stroke1 = new SolidColorBrush(Color.FromRgb(0, 200, 255));   // Cyan
                var stroke2 = new SolidColorBrush(Color.FromRgb(255, 165, 0));   // Orange
                var fillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 200, 255));

                var c1 = CodingNormToPixel(pipeDir.Center1);
                var c2 = CodingNormToPixel(pipeDir.Center2);

                double canvasW = CodingOverlayCanvas.ActualWidth;
                double canvasH = CodingOverlayCanvas.ActualHeight;

                // Ellipse 1 (Rohrverbindung — Cyan)
                double rx1Px = pipeDir.RadiusX1 * canvasW;
                double ry1Px = pipeDir.RadiusY1 * canvasH;
                var ellipse1 = new System.Windows.Shapes.Ellipse
                {
                    Width = rx1Px * 2, Height = ry1Px * 2,
                    Stroke = stroke1, StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Fill = fillBrush,
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                Canvas.SetLeft(ellipse1, c1.X - rx1Px);
                Canvas.SetTop(ellipse1, c1.Y - ry1Px);
                CodingOverlayCanvas.Children.Add(ellipse1);

                // Ellipse 2 (weiter im Rohr — Orange)
                double rx2Px = pipeDir.RadiusX2 * canvasW;
                double ry2Px = pipeDir.RadiusY2 * canvasH;
                var ellipse2 = new System.Windows.Shapes.Ellipse
                {
                    Width = rx2Px * 2, Height = ry2Px * 2,
                    Stroke = stroke2, StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                Canvas.SetLeft(ellipse2, c2.X - rx2Px);
                Canvas.SetTop(ellipse2, c2.Y - ry2Px);
                CodingOverlayCanvas.Children.Add(ellipse2);

                // Verbindungslinie (Richtungswechsel)
                var connector = new System.Windows.Shapes.Line
                {
                    X1 = c1.X, Y1 = c1.Y, X2 = c2.X, Y2 = c2.Y,
                    Stroke = Brushes.White, StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(connector);

                // Handles
                AddDotMarker(c1, 7, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(c2, 7, stroke2, "overlay_preview", glowEffect);

                // Groessen-Handles (kleine Punkte an den Ellipsenraendern)
                AddDotMarker(new Point(c1.X + rx1Px, c1.Y), 4, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c1.X, c1.Y + ry1Px), 4, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c2.X + rx2Px, c2.Y), 4, stroke2, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c2.X, c2.Y + ry2Px), 4, stroke2, "overlay_preview", glowEffect);

                // Winkel-Label
                var midPoint = new Point((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);
                AddSchemaLabel(midPoint, $"{pipeDir.AngleDeg:F0}°", Brushes.White, glowEffect);
                break;
            }
        }
    }

    private void RenderSchemaPipeReference(
        NormalizedPoint centerNorm,
        double radiusNorm,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        var center = CodingNormToPixel(centerNorm);
        // Kreisprofil: Radius in Pixel basierend auf Canvas-Hoehe
        // (Hoehe ist die kuerzere Dimension, damit der Kreis immer rund bleibt)
        double rPx = radiusNorm * Math.Min(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight);

        var pipe = new System.Windows.Shapes.Ellipse
        {
            Width = rPx * 2,
            Height = rPx * 2,
            Stroke = stroke,
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Effect = glowEffect,
            Tag = tag
        };
        Canvas.SetLeft(pipe, center.X - rPx);
        Canvas.SetTop(pipe, center.Y - rPx);
        CodingOverlayCanvas.Children.Add(pipe);
    }

    private void AddSchemaLabel(
        Point anchor,
        string text,
        Brush foreground,
        System.Windows.Media.Effects.DropShadowEffect glowEffect)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            Background = new SolidColorBrush(Color.FromArgb(205, 17, 19, 24)),
            Padding = new Thickness(6, 3, 6, 3),
            Effect = glowEffect,
            Tag = "overlay_measure"
        };
        Canvas.SetLeft(label, anchor.X + 12);
        Canvas.SetTop(label, anchor.Y - 20);
        CodingOverlayCanvas.Children.Add(label);
    }
}
