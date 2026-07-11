using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Kleine Trend-Kurve fuer Kacheln und Statusbloecke (Ersatz fuer Text-Pfeile).
/// Zeichnet direkt in OnRender (StreamGeometry) — leichtgewichtig, ohne Layout-Kosten.
/// Werte kommen als beliebige IEnumerable von Zahlen; NaN wird gefiltert,
/// unter 2 Punkten wird nichts gezeichnet.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values), typeof(IEnumerable), typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke), typeof(Brush), typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowAreaProperty =
        DependencyProperty.Register(
            nameof(ShowArea), typeof(bool), typeof(Sparkline),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public Sparkline()
    {
        MinHeight = 18;
        MinWidth = 40;
    }

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public bool ShowArea
    {
        get => (bool)GetValue(ShowAreaProperty);
        set => SetValue(ShowAreaProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var werte = LeseWerte(Values);
        if (werte.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var min = werte.Min();
        var max = werte.Max();
        var spanne = Math.Max(1e-9, max - min);

        // 2px Innenabstand, damit die Kurve nicht am Rand klebt.
        const double rand = 2d;
        var breite = ActualWidth - 2 * rand;
        var hoehe = ActualHeight - 2 * rand;

        Point PunktFuer(int index) => new(
            rand + breite * index / (werte.Count - 1),
            rand + hoehe * (1d - (werte[index] - min) / spanne));

        var stroke = Stroke ?? TryFindResource("AccentBrush") as Brush ?? Brushes.SteelBlue;

        var linie = new StreamGeometry();
        using (var ctx = linie.Open())
        {
            ctx.BeginFigure(PunktFuer(0), isFilled: false, isClosed: false);
            for (var i = 1; i < werte.Count; i++)
                ctx.LineTo(PunktFuer(i), isStroked: true, isSmoothJoin: true);
        }
        linie.Freeze();

        if (ShowArea && stroke is SolidColorBrush solid)
        {
            var flaeche = new StreamGeometry();
            using (var ctx = flaeche.Open())
            {
                ctx.BeginFigure(new Point(rand, ActualHeight - rand), isFilled: true, isClosed: true);
                for (var i = 0; i < werte.Count; i++)
                    ctx.LineTo(PunktFuer(i), isStroked: false, isSmoothJoin: true);
                ctx.LineTo(new Point(ActualWidth - rand, ActualHeight - rand), isStroked: false, isSmoothJoin: false);
            }
            flaeche.Freeze();

            var verlauf = new LinearGradientBrush(
                Color.FromArgb(70, solid.Color.R, solid.Color.G, solid.Color.B),
                Color.FromArgb(0, solid.Color.R, solid.Color.G, solid.Color.B),
                90d);
            verlauf.Freeze();
            dc.DrawGeometry(verlauf, null, flaeche);
        }

        var stift = new Pen(stroke, 1.6) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        stift.Freeze();
        dc.DrawGeometry(null, stift, linie);

        // Endpunkt betonen: der letzte Wert ist die Aussage der Kurve.
        dc.DrawEllipse(stroke, null, PunktFuer(werte.Count - 1), 2.2, 2.2);
    }

    private static IReadOnlyList<double> LeseWerte(IEnumerable? quelle)
    {
        if (quelle is null)
            return [];

        var werte = new List<double>();
        foreach (var roh in quelle)
        {
            if (roh is null)
                continue;
            try
            {
                var wert = Convert.ToDouble(roh, CultureInfo.InvariantCulture);
                if (!double.IsNaN(wert) && !double.IsInfinity(wert))
                    werte.Add(wert);
            }
            catch
            {
                // Nicht-numerische Eintraege still ueberspringen.
            }
        }

        return werte;
    }
}
