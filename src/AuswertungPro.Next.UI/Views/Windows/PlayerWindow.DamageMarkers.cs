using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: DamageMarker-Overlay extrahiert aus PlayerWindow.xaml.cs.
// Visualisiert Befunde auf der Position-Slider-Spur (Punkt- und
// Strecken-Marker mit Code-Label + Tooltip + Click-to-Seek).
public partial class PlayerWindow
{
    private void BuildDamageMarkers()
    {
        if (_damageOverlay is null)
            return;

        DamageMarkerCanvas.Children.Clear();
        _damageMarkers.Clear();

        var accentBrush = (Brush)FindResource("AccentBrush");
        var accentColor = (Color)FindResource("ColorAccent");

        foreach (var info in _damageOverlay.Markers)
        {
            if (!info.TimeStart.HasValue)
                continue;

            if (info.IsStreckenschaden
                && info.TimeEnd.HasValue
                && info.TimeEnd.Value > info.TimeStart.Value)
                CreateRangeMarker(info, accentBrush, accentColor);
            else
                CreatePointMarker(info, accentBrush, accentColor);
        }

        RepositionDamageMarkers();
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

        container.ToolTip = BuildDamageMarkerTooltip(info, isRange: false);

        container.MouseLeftButtonDown += (_, _) => SeekToMarker(info);

        DamageMarkerCanvas.Children.Add(container);
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

        var endM = info.MeterEnd ?? info.MeterStart;
        if (_damageOverlay!.PipeLengthMeters > 0)
            endM = Math.Min(endM, _damageOverlay.PipeLengthMeters);
        container.ToolTip = BuildDamageMarkerTooltip(info, isRange: true, endMeter: endM);

        container.MouseLeftButtonDown += (_, _) => SeekToMarker(info);

        DamageMarkerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, bar, label));
    }

    private void RepositionDamageMarkers()
    {
        if (_damageOverlay is null || _damageMarkers.Count == 0)
            return;

        var (offsetX, trackWidth) = GetSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        foreach (var (info, container, tickOrRange, label) in _damageMarkers)
        {
            if (!TryGetMarkerStartRatio(info, out var ratio))
            {
                container.Visibility = Visibility.Collapsed;
                continue;
            }

            container.Visibility = Visibility.Visible;
            var x = offsetX + ratio * trackWidth;

            if (info.IsStreckenschaden
                && info.TimeEnd.HasValue
                && info.TimeStart.HasValue
                && info.TimeEnd.Value > info.TimeStart.Value)
            {
                Canvas.SetLeft(container, x);
                var endRatio = TryGetMarkerEndRatio(info, out var markerEndRatio)
                    ? markerEndRatio
                    : ratio;
                var endX = offsetX + endRatio * trackWidth;
                var barWidth = Math.Max(endX - x, 3);
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

    private string BuildDamageMarkerTooltip(DamageMarkerInfo info, bool isRange, double? endMeter = null)
    {
        var text = isRange
            ? $"{info.Code} Strecke {info.MeterStart:0.0}m - {(endMeter ?? info.MeterEnd ?? info.MeterStart):0.0}m"
            : $"{info.Code} @ {info.MeterStart:0.0}m";

        if (info.TimeStart.HasValue)
            text += $"\nZeit {FormatMs((long)Math.Max(0, info.TimeStart.Value.TotalMilliseconds))}";

        if (!string.IsNullOrWhiteSpace(info.Description))
            text += $"\n{info.Description}";

        return text;
    }

    private bool TryGetMarkerStartRatio(DamageMarkerInfo info, out double ratio)
        => TryGetTimelineRatio(info.TimeStart, out ratio);

    private bool TryGetMarkerEndRatio(DamageMarkerInfo info, out double ratio)
        => TryGetTimelineRatio(info.TimeEnd, out ratio);

    private bool TryGetTimelineRatio(TimeSpan? time, out double ratio)
    {
        ratio = 0;
        if (!time.HasValue)
            return false;

        var length = _player.Length;
        if (length <= 0)
            return false;

        var ms = time.Value.TotalMilliseconds;
        if (ms < 0 || ms > length + 1000)
            return false;

        ratio = Math.Clamp(ms / length, 0.0, 1.0);
        return true;
    }

    private void SeekToMarker(DamageMarkerInfo info)
        => TrySeekToMarkerTime(info);

    private bool TrySeekToMarkerTime(DamageMarkerInfo info)
    {
        if (!info.TimeStart.HasValue)
            return false;

        var length = _player.Length;
        if (length <= 0)
            return false;

        var ms = info.TimeStart.Value.TotalMilliseconds;
        if (ms < 0 || ms > length + 1000)
            return false;

        EnsurePlaying();
        // Pause so the jumped-to frame is clearly visible
        _player.SetPause(true);

        var targetMs = (long)Math.Max(0, ms);
        _player.Time = targetMs;

        var ratio = Math.Clamp((double)targetMs / length, 0.0, 1.0);
        PositionSlider.Value = ratio * PositionSlider.Maximum;

        UpdateUi();
        return true;
    }

}
