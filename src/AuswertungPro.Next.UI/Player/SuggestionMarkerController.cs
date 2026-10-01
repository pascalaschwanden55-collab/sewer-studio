using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Zeichnet die KI-Vorschlaege als kleine Marker unter der Zeitleiste. Eigene
/// Flaeche und zweite Farbe (SecondaryAccentBrush), damit sie sich von den
/// Befundmarkern unterscheiden. Klick springt zur Videozeit.
/// </summary>
public sealed class SuggestionMarkerController
{
    private readonly Canvas _canvas;
    private readonly Func<(double offsetX, double trackWidth)> _getBounds;
    private readonly Func<double?> _getDurationSeconds;
    private readonly Action<double> _seekToSeconds;
    private readonly List<(CodingSuggestionRow Row, FrameworkElement Element)> _marker = new();

    public SuggestionMarkerController(
        Canvas canvas,
        Func<(double offsetX, double trackWidth)> getBounds,
        Func<double?> getDurationSeconds,
        Action<double> seekToSeconds)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _getBounds = getBounds ?? throw new ArgumentNullException(nameof(getBounds));
        _getDurationSeconds = getDurationSeconds ?? throw new ArgumentNullException(nameof(getDurationSeconds));
        _seekToSeconds = seekToSeconds ?? throw new ArgumentNullException(nameof(seekToSeconds));
    }

    public void Build(IReadOnlyList<CodingSuggestionRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Clear();

        var brush = _canvas.TryFindResource("SecondaryAccentBrush") as Brush ?? Brushes.Gray;
        var dauer = _getDurationSeconds() ?? 0.0;
        var (offsetX, trackWidth) = _getBounds();

        foreach (var row in rows)
        {
            if (SuggestionMarkerLayout.CalculateX(row.TimeSeconds, dauer, offsetX, trackWidth) is not { } x)
                continue;

            var tick = new Rectangle
            {
                Width = 3,
                Height = 8,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = brush,
                Opacity = row.IsConfirmed ? 0.35 : 0.9,
                Cursor = Cursors.Hand,
                ToolTip = row.Text
            };
            var zeit = row.TimeSeconds;
            tick.MouseLeftButtonDown += (_, _) => _seekToSeconds(zeit);
            Canvas.SetLeft(tick, x - 1);
            Canvas.SetTop(tick, 0);
            _canvas.Children.Add(tick);
            _marker.Add((row, tick));
        }
    }

    public void Reposition()
    {
        var dauer = _getDurationSeconds() ?? 0.0;
        var (offsetX, trackWidth) = _getBounds();
        foreach (var (row, element) in _marker)
        {
            if (SuggestionMarkerLayout.CalculateX(row.TimeSeconds, dauer, offsetX, trackWidth) is { } x)
                Canvas.SetLeft(element, x - 1);
        }
    }

    public void Clear()
    {
        foreach (var (_, element) in _marker)
            _canvas.Children.Remove(element);
        _marker.Clear();
    }
}
