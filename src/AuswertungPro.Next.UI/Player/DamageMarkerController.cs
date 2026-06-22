using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibVLCSharp.Shared;
using Rectangle = System.Windows.Shapes.Rectangle;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer; // disambig: sonst Konflikt mit System.Windows.Media.MediaPlayer

namespace AuswertungPro.Next.UI.Player;

public sealed class DamageMarkerController
{
    private readonly Canvas _markerCanvas;
    private readonly Slider _positionSlider;
    private readonly PlayerDamageOverlayData? _damageOverlay;
    private readonly MediaPlayer _player;
    private readonly Action _ensurePlaying;
    private readonly Action _updateUi;
    private readonly Func<(double offsetX, double trackWidth)> _getSliderTrackBounds;
    private readonly List<(DamageMarkerInfo Info, FrameworkElement Container, FrameworkElement TickOrRange, TextBlock Label)> _damageMarkers = new();

    public DamageMarkerController(
        Canvas markerCanvas,
        Slider positionSlider,
        PlayerDamageOverlayData? damageOverlay,
        MediaPlayer player,
        Action ensurePlaying,
        Action updateUi,
        Func<(double offsetX, double trackWidth)> getSliderTrackBounds)
    {
        _markerCanvas = markerCanvas ?? throw new ArgumentNullException(nameof(markerCanvas));
        _positionSlider = positionSlider ?? throw new ArgumentNullException(nameof(positionSlider));
        _damageOverlay = damageOverlay;
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _ensurePlaying = ensurePlaying ?? throw new ArgumentNullException(nameof(ensurePlaying));
        _updateUi = updateUi ?? throw new ArgumentNullException(nameof(updateUi));
        _getSliderTrackBounds = getSliderTrackBounds ?? throw new ArgumentNullException(nameof(getSliderTrackBounds));
    }

    public void Build()
    {
        if (_damageOverlay is null || _damageOverlay.PipeLengthMeters <= 0)
            return;

        _markerCanvas.Children.Clear();
        _damageMarkers.Clear();

        var accentBrush = (Brush)_markerCanvas.FindResource("AccentBrush");
        var accentColor = (Color)_markerCanvas.FindResource("ColorAccent");

        foreach (var info in _damageOverlay.Markers)
        {
            if (info.MeterStart < 0 || info.MeterStart > _damageOverlay.PipeLengthMeters)
                continue;

            if (info.IsStreckenschaden && info.MeterEnd.HasValue && info.MeterEnd.Value > info.MeterStart)
                CreateRangeMarker(info, accentBrush, accentColor);
            else
                CreatePointMarker(info, accentBrush, accentColor);
        }

        Reposition();
    }

    public void Reposition()
    {
        if (_damageOverlay is null || _damageMarkers.Count == 0)
            return;

        var (offsetX, trackWidth) = _getSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        var pipeLength = _damageOverlay.PipeLengthMeters;

        foreach (var (info, container, tickOrRange, label) in _damageMarkers)
        {
            var x = PlayerTimelineLayoutCalculator.CalculatePointX(
                info.MeterStart,
                pipeLength,
                offsetX,
                trackWidth);

            if (info.IsStreckenschaden && info.MeterEnd.HasValue && info.MeterEnd.Value > info.MeterStart)
            {
                var range = PlayerTimelineLayoutCalculator.CalculateRangeX(
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

    private void CreatePointMarker(DamageMarkerInfo info, Brush accentBrush, Color accentColor)
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
            FontFamily = new FontFamily("Consolas"),
            Foreground = accentBrush,
            IsHitTestVisible = false
        };
        Canvas.SetTop(label, -19);
        container.Children.Add(label);

        container.ToolTip = $"{info.Code} @ {info.MeterStart:0.0}m"
            + (string.IsNullOrWhiteSpace(info.Description) ? "" : $"\n{info.Description}");

        container.MouseLeftButtonDown += (_, _) => SeekToMeter(info.MeterStart);

        _markerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, tick, label));
    }

    private void CreateRangeMarker(DamageMarkerInfo info, Brush accentBrush, Color accentColor)
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
            FontFamily = new FontFamily("Consolas"),
            Foreground = accentBrush,
            IsHitTestVisible = false
        };
        Canvas.SetTop(label, -19);
        container.Children.Add(label);

        var endM = Math.Min(info.MeterEnd ?? info.MeterStart, _damageOverlay!.PipeLengthMeters);
        container.ToolTip = $"{info.Code} Strecke {info.MeterStart:0.0}m - {endM:0.0}m"
            + (string.IsNullOrWhiteSpace(info.Description) ? "" : $"\n{info.Description}");

        container.MouseLeftButtonDown += (_, _) => SeekToMeter(info.MeterStart);

        _markerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, bar, label));
    }

    private void SeekToMeter(double meter)
    {
        if (_damageOverlay is null || _damageOverlay.PipeLengthMeters <= 0)
            return;

        _ensurePlaying();
        _player.SetPause(true);

        var ratio = Math.Clamp(meter / _damageOverlay.PipeLengthMeters, 0.0, 1.0);
        _positionSlider.Value = ratio * _positionSlider.Maximum;

        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(ratio * length);
        else
            _player.Position = (float)ratio;

        _updateUi();
    }
}
