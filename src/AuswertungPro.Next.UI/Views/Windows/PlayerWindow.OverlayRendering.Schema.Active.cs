using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

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
                    RenderPipeBendOverlay(overlay, true, Brushes.Gold, glowEffect, OverlayTags.Preview, bend.Center);

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
                    Tag = OverlayTags.Preview
                };
                CodingOverlayCanvas.Children.Add(guide);

                AddDotMarker(radiusHandle, 5, Brushes.White, OverlayTags.Preview, glowEffect);
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

                RenderSchemaPipeReference(fill.PipeCenter, fill.PipeRadius, stroke, glowEffect, OverlayTags.Preview);

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
                    Tag = OverlayTags.Preview,
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
                    Tag = OverlayTags.Preview
                };
                CodingOverlayCanvas.Children.Add(levelLine);

                AddDotMarker(new Point(center.X, levelY), 6, stroke, OverlayTags.Preview, glowEffect);
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

                RenderSchemaPipeReference(intrusion.PipeCenter, intrusion.PipeRadius, stroke, glowEffect, OverlayTags.Preview);

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
                    Tag = OverlayTags.Preview
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
                    Tag = OverlayTags.Preview
                };
                CodingOverlayCanvas.Children.Add(spine);

                AddDotMarker(tip, 7, stroke, OverlayTags.Preview, glowEffect);
                AddDotMarker(edge, 5, Brushes.White, OverlayTags.Preview, glowEffect);
                AddSchemaLabel(tip, $"{overlay.FillPercent:F1}% @ {overlay.ClockFrom:F1}h", stroke, glowEffect);
                break;
            }
        }
    }
}
