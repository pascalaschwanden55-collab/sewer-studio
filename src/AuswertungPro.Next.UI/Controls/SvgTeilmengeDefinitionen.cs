using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Xml.Linq;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Der <c>&lt;defs&gt;</c>-Teil der SVG-Teilmenge: Verlaeufe, Muster, Filter und Zuschnitte.
/// Diese Inhalte brauchen fertige Pinsel, deshalb liest <see cref="SvgFarbZuordnung.Pinsel"/>
/// den Theme-Token hier einmalig aus (ein <see cref="Freezable"/> kennt keine Ressourcensuche).
///
/// Filter: SVG kennt eine Filterkette, WPF nur fertige Effekte. Ein <c>feDropShadow</c> wird zum
/// <see cref="DropShadowEffect"/>; jede andere bekannte Kette (etwa der Leuchtfilter am
/// Fliesspfeil) wird bewusst weggelassen statt falsch nachgebaut.
/// </summary>
internal sealed class SvgTeilmengeDefinitionen
{
    private readonly FrameworkElement _quelle;
    private readonly Dictionary<string, Brush> _pinsel = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Effect?> _effekte = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect> _zuschnitte = new(StringComparer.Ordinal);

    public SvgTeilmengeDefinitionen(FrameworkElement quelle) => _quelle = quelle;

    /// <summary>Liest einen <c>defs</c>-Block samt seiner Kinder.</summary>
    public void Lies(XElement defs, double breite, double hoehe)
    {
        foreach (var kind in defs.Elements())
        {
            SvgTeilmengeZeichner.PruefeElement(kind);
            var id = (string?)kind.Attribute("id") ?? string.Empty;
            switch (kind.Name.LocalName)
            {
                case "linearGradient":
                    _pinsel[id] = Verlauf(kind);
                    break;
                case "pattern":
                    _pinsel[id] = Muster(kind, breite, hoehe);
                    break;
                case "filter":
                    _effekte[id] = Filter(kind);
                    break;
                case "clipPath":
                    _zuschnitte[id] = Zuschnitt(kind, breite, hoehe);
                    break;
                default:
                    throw new NotSupportedException(
                        $"SVG-Teilmenge: '{kind.Name.LocalName}' ist in <defs> nicht vorgesehen.");
            }
        }
    }

    public Brush? Pinsel(string id) => _pinsel.TryGetValue(id, out var p) ? p : null;

    public Effect? Effekt(string id) => _effekte.TryGetValue(id, out var e) ? e : null;

    public Rect? Zuschnitt(string id) => _zuschnitte.TryGetValue(id, out var r) ? r : null;

    private Brush Verlauf(XElement element)
    {
        var pinsel = new LinearGradientBrush
        {
            StartPoint = new Point(SvgWert.Zahl(element, "x1", 0), SvgWert.Zahl(element, "y1", 0)),
            EndPoint = new Point(SvgWert.Zahl(element, "x2", 1), SvgWert.Zahl(element, "y2", 0))
        };

        foreach (var stop in element.Elements())
        {
            SvgTeilmengeZeichner.PruefeElement(stop);
            if (stop.Name.LocalName != "stop")
            {
                throw new NotSupportedException(
                    $"SVG-Teilmenge: '{stop.Name.LocalName}' ist in <linearGradient> nicht vorgesehen.");
            }

            var farbe = Farbe(
                (string?)stop.Attribute("stop-color"),
                SvgWert.ZahlOderProzent((string?)stop.Attribute("stop-opacity"), 1));
            pinsel.GradientStops.Add(new GradientStop(
                farbe,
                SvgWert.ZahlOderProzent((string?)stop.Attribute("offset"), 0, 1)));
        }

        pinsel.Freeze();
        return pinsel;
    }

    private Brush Muster(XElement element, double breite, double hoehe)
    {
        var kachelBreite = Math.Max(1d, SvgWert.Zahl(element, "width", 8, breite));
        var kachelHoehe = Math.Max(1d, SvgWert.Zahl(element, "height", 8, hoehe));

        var gruppe = new DrawingGroup();
        foreach (var kind in element.Elements())
        {
            SvgTeilmengeZeichner.PruefeElement(kind);
            gruppe.Children.Add(MusterInhalt(kind, kachelBreite, kachelHoehe));
        }

        var pinsel = new DrawingBrush(gruppe)
        {
            TileMode = TileMode.Tile,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, kachelBreite, kachelHoehe),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, kachelBreite, kachelHoehe),
            Stretch = Stretch.Fill
        };

        if (SvgWert.Transform((string?)element.Attribute("patternTransform")) is { } transform)
            pinsel.Transform = transform;

        pinsel.Freeze();
        return pinsel;
    }

    private Drawing MusterInhalt(XElement kind, double breite, double hoehe)
    {
        switch (kind.Name.LocalName)
        {
            case "rect":
                var fuellung = SvgTeilmengeZeichner.IstOhneFarbe((string?)kind.Attribute("fill"))
                    ? null
                    : SvgFarbZuordnung.Pinsel(_quelle, (string?)kind.Attribute("fill"));
                return new GeometryDrawing(
                    fuellung,
                    null,
                    new RectangleGeometry(new Rect(
                        SvgWert.Zahl(kind, "x", 0, breite),
                        SvgWert.Zahl(kind, "y", 0, hoehe),
                        Math.Max(0d, SvgWert.Zahl(kind, "width", 0, breite)),
                        Math.Max(0d, SvgWert.Zahl(kind, "height", 0, hoehe)))));

            case "line":
                var stift = new Pen(
                    SvgFarbZuordnung.Pinsel(_quelle, (string?)kind.Attribute("stroke")),
                    SvgWert.Zahl(kind, "stroke-width", 1));
                stift.Freeze();
                return new GeometryDrawing(null, stift, new LineGeometry(
                    new Point(SvgWert.Zahl(kind, "x1", 0), SvgWert.Zahl(kind, "y1", 0)),
                    new Point(SvgWert.Zahl(kind, "x2", 0), SvgWert.Zahl(kind, "y2", 0))));

            default:
                throw new NotSupportedException(
                    $"SVG-Teilmenge: '{kind.Name.LocalName}' ist in <pattern> nicht vorgesehen.");
        }
    }

    private Effect? Filter(XElement element)
    {
        foreach (var kind in element.Elements())
        {
            SvgTeilmengeZeichner.PruefeElement(kind);
            foreach (var enkel in kind.Elements())
                SvgTeilmengeZeichner.PruefeElement(enkel);
        }

        var schatten = element.Elements().FirstOrDefault(k => k.Name.LocalName == "feDropShadow");
        if (schatten is null)
            return null;

        var dx = SvgWert.Zahl(schatten, "dx", 0);
        var dy = SvgWert.Zahl(schatten, "dy", 0);
        var farbe = Farbe((string?)schatten.Attribute("flood-color"), 1);
        var effekt = new DropShadowEffect
        {
            BlurRadius = Math.Max(0d, SvgWert.Zahl(schatten, "stdDeviation", 1) * 2d),
            ShadowDepth = Math.Sqrt(dx * dx + dy * dy),
            Direction = dx == 0 && dy == 0 ? 315d : (Math.Atan2(-dy, dx) * 180d / Math.PI + 360d) % 360d,
            Color = Color.FromRgb(farbe.R, farbe.G, farbe.B),
            Opacity = farbe.A / 255d
        };
        effekt.Freeze();
        return effekt;
    }

    private static Rect Zuschnitt(XElement element, double breite, double hoehe)
    {
        foreach (var kind in element.Elements())
        {
            SvgTeilmengeZeichner.PruefeElement(kind);
            if (kind.Name.LocalName != "rect")
            {
                throw new NotSupportedException(
                    $"SVG-Teilmenge: '{kind.Name.LocalName}' ist in <clipPath> nicht vorgesehen.");
            }
        }

        var rechteck = element.Elements().FirstOrDefault(k => k.Name.LocalName == "rect");
        if (rechteck is null)
            return new Rect(0, 0, breite, hoehe);

        return new Rect(
            SvgWert.Zahl(rechteck, "x", 0, breite),
            SvgWert.Zahl(rechteck, "y", 0, hoehe),
            Math.Max(0d, SvgWert.Zahl(rechteck, "width", 0, breite)),
            Math.Max(0d, SvgWert.Zahl(rechteck, "height", 0, hoehe)));
    }

    /// <summary>
    /// Farbe aus dem Theme-Token samt Deckung. Ein achtstelliger Hexwert wie <c>#00000033</c>
    /// traegt seine Deckung selbst; er kommt nur in Filtern vor und meint dort keine
    /// Oberflaechenfarbe, sondern einen Schatten.
    /// </summary>
    private Color Farbe(string? farbe, double deckung)
    {
        var text = SvgFarbZuordnung.Normalisiere(farbe);
        Color grund;
        var eigene = 1d;

        if (text.Length == 9 && text[0] == '#')
        {
            var voll = (Color)ColorConverter.ConvertFromString("#" + text.Substring(7, 2) + text.Substring(1, 6));
            grund = voll;
            eigene = voll.A / 255d;
        }
        else if (SvgFarbZuordnung.Pinsel(_quelle, text) is SolidColorBrush einfarbig)
        {
            grund = einfarbig.Color;
            eigene = einfarbig.Opacity;
        }
        else
        {
            grund = Colors.Gray;
        }

        var alpha = (byte)Math.Clamp(
            Math.Round(255d * Math.Clamp(deckung, 0d, 1d) * Math.Clamp(eigene, 0d, 1d)),
            0d,
            255d);
        return Color.FromArgb(alpha, grund.R, grund.G, grund.B);
    }
}
