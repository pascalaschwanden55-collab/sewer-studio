using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void BuildDamageMarkers()
    {
        if (_damageOverlay is null || _damageOverlay.PipeLengthMeters <= 0)
            return;

        DamageMarkerCanvas.Children.Clear();
        _damageMarkers.Clear();

        var accentBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
        var accentColor = (System.Windows.Media.Color)FindResource("ColorAccent");

        foreach (var info in _damageOverlay.Markers)
        {
            if (info.MeterStart < 0 || info.MeterStart > _damageOverlay.PipeLengthMeters)
                continue;

            if (info.IsStreckenschaden && info.MeterEnd.HasValue && info.MeterEnd.Value > info.MeterStart)
                CreateRangeMarker(info, accentBrush, accentColor);
            else
                CreatePointMarker(info, accentBrush, accentColor);
        }

        RepositionDamageMarkers();
    }

    private void CreatePointMarker(DamageMarkerInfo info, System.Windows.Media.Brush accentBrush, System.Windows.Media.Color accentColor)
    {
        var container = new Canvas { Cursor = Cursors.Hand };

        var tick = new Rectangle
        {
            Width = 2,
            Height = 14,
            Fill = accentBrush,
            Opacity = 0.85,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = accentColor, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.5 }
        };
        Canvas.SetTop(tick, -5);
        container.Children.Add(tick);

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(info.Code) ? "?" : info.Code.Trim(),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Foreground = accentBrush,
            IsHitTestVisible = false
        };
        Canvas.SetTop(label, -19);
        container.Children.Add(label);

        container.ToolTip = $"{info.Code} @ {info.MeterStart:0.0}m"
            + (string.IsNullOrWhiteSpace(info.Description) ? "" : $"\n{info.Description}");

        container.MouseLeftButtonDown += (_, _) => SeekToMeter(info.MeterStart);

        DamageMarkerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, tick, label));
    }

    private void CreateRangeMarker(DamageMarkerInfo info, System.Windows.Media.Brush accentBrush, System.Windows.Media.Color accentColor)
    {
        var container = new Canvas { Cursor = Cursors.Hand };

        var bar = new Rectangle
        {
            Height = 5,
            Fill = accentBrush,
            Opacity = 0.35,
            RadiusX = 2,
            RadiusY = 2,
            IsHitTestVisible = false
        };
        Canvas.SetTop(bar, -2);
        container.Children.Add(bar);

        var startTick = new Rectangle
        {
            Width = 1.5,
            Height = 10,
            Fill = accentBrush,
            Opacity = 0.7,
            IsHitTestVisible = false
        };
        Canvas.SetTop(startTick, -4);
        container.Children.Add(startTick);

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(info.Code) ? "?" : info.Code.Trim(),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Foreground = accentBrush,
            IsHitTestVisible = false
        };
        Canvas.SetTop(label, -19);
        container.Children.Add(label);

        var endM = Math.Min(info.MeterEnd ?? info.MeterStart, _damageOverlay!.PipeLengthMeters);
        container.ToolTip = $"{info.Code} Strecke {info.MeterStart:0.0}m - {endM:0.0}m"
            + (string.IsNullOrWhiteSpace(info.Description) ? "" : $"\n{info.Description}");

        container.MouseLeftButtonDown += (_, _) => SeekToMeter(info.MeterStart);

        DamageMarkerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, bar, label));
    }

    private (double offsetX, double trackWidth) GetSliderTrackBounds()
    {
        if (PositionSlider.Template?.FindName("PART_Track", PositionSlider) is Track track
            && track.IsVisible
            && track.ActualWidth > 0)
        {
            var thumbHalf = (track.Thumb?.ActualWidth ?? 18) / 2.0;
            var ptStart = track.TranslatePoint(new Point(thumbHalf, 0), DamageMarkerCanvas);
            var ptEnd = track.TranslatePoint(new Point(track.ActualWidth - thumbHalf, 0), DamageMarkerCanvas);
            return (ptStart.X, ptEnd.X - ptStart.X);
        }

        return (9, Math.Max(DamageMarkerCanvas.ActualWidth - 18, 1));
    }

    private void RepositionDamageMarkers()
    {
        if (_damageOverlay is null || _damageMarkers.Count == 0)
            return;

        var (offsetX, trackWidth) = GetSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        var pipeLength = _damageOverlay.PipeLengthMeters;

        foreach (var (info, container, tickOrRange, label) in _damageMarkers)
        {
            var x = AuswertungPro.Next.UI.Player.PlayerTimelineLayoutCalculator.CalculatePointX(
                info.MeterStart,
                pipeLength,
                offsetX,
                trackWidth);

            if (info.IsStreckenschaden && info.MeterEnd.HasValue && info.MeterEnd.Value > info.MeterStart)
            {
                var range = AuswertungPro.Next.UI.Player.PlayerTimelineLayoutCalculator.CalculateRangeX(
                    info.MeterStart,
                    info.MeterEnd.Value,
                    pipeLength,
                    offsetX,
                    trackWidth);
                Canvas.SetLeft(container, range.StartX);
                var barWidth = Math.Max(range.Width, 3);
                ((Rectangle)tickOrRange).Width = barWidth;

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var labelWidth = label.DesiredSize.Width;
                Canvas.SetLeft(label, (barWidth - labelWidth) / 2);
            }
            else
            {
                Canvas.SetLeft(container, x - 1);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var labelWidth = label.DesiredSize.Width;
                Canvas.SetLeft(label, -(labelWidth / 2) + 1);
            }
        }
    }

    private void SeekToMeter(double meter)
    {
        if (_damageOverlay is null || _damageOverlay.PipeLengthMeters <= 0)
            return;

        EnsurePlaying();
        _player.SetPause(true);

        var ratio = Math.Clamp(meter / _damageOverlay.PipeLengthMeters, 0.0, 1.0);
        PositionSlider.Value = ratio * PositionSlider.Maximum;

        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(ratio * length);
        else
            _player.Position = (float)ratio;

        UpdateUi();
    }
}
