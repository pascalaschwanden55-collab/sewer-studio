using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Zeichnet die SVG-Teilmenge unserer eigenen Grafikbauer (heute
/// <c>HaltungsgrafikSvgBuilder</c>) als WPF-Formen auf eine <see cref="Canvas"/>. Das Programm
/// hat keinen allgemeinen SVG-Renderer und soll auch keinen bekommen: Zugelassen ist genau
/// das, was unsere Bauer wirklich schreiben.
///
/// Der Vertrag ist bewusst streng: Ein unbekanntes Element oder Attribut wirft eine
/// <see cref="NotSupportedException"/> mit Namen, statt still nichts zu zeichnen. Wer im Bauer
/// ein neues SVG-Element einfuehrt, muss den Zeichner erweitern — sonst faellt der Waechter
/// <c>HaltungsgrafikSvgBuilderTeilmengeTests</c> um, nicht erst der Benutzer.
///
/// Farben kommen ausschliesslich aus <see cref="SvgFarbZuordnung"/>; im Zeichner selbst steht
/// keine Farbe. Flaechen- und Strichfarben haengen ueber <c>SetResourceReference</c> am Theme
/// und folgen deshalb einem Designwechsel ohne Neuzeichnen.
/// </summary>
public static class SvgTeilmengeZeichner
{
    private static readonly string[] KeineAttribute = [];

    private static readonly string[] Formattribute =
        ["fill", "stroke", "stroke-width", "opacity", "transform", "clip-path", "filter"];

    private static readonly Dictionary<string, string[]> Erlaubt = new(StringComparer.Ordinal)
    {
        ["svg"] = ["width", "height", "viewBox"],
        ["defs"] = KeineAttribute,
        ["linearGradient"] = ["id", "x1", "y1", "x2", "y2"],
        ["stop"] = ["offset", "stop-color", "stop-opacity"],
        ["pattern"] = ["id", "patternUnits", "width", "height", "patternTransform"],
        ["filter"] = ["id", "x", "y", "width", "height"],
        ["feDropShadow"] = ["dx", "dy", "stdDeviation", "flood-color", "flood-opacity"],
        ["feGaussianBlur"] = ["in", "stdDeviation", "result"],
        ["feFlood"] = ["flood-color", "flood-opacity", "result"],
        ["feComposite"] = ["in", "in2", "operator", "result"],
        ["feMerge"] = KeineAttribute,
        ["feMergeNode"] = ["in"],
        ["clipPath"] = ["id"],
        ["g"] = ["transform"],
        ["rect"] = [.. Formattribute, "x", "y", "width", "height", "rx", "ry"],
        ["line"] = [.. Formattribute, "x1", "y1", "x2", "y2", "stroke-dasharray", "stroke-linecap"],
        ["circle"] = [.. Formattribute, "cx", "cy", "r"],
        ["ellipse"] = [.. Formattribute, "cx", "cy", "rx", "ry"],
        ["polygon"] = [.. Formattribute, "points", "stroke-linecap", "stroke-linejoin"],
        ["polyline"] = [.. Formattribute, "points", "stroke-linecap", "stroke-linejoin"],
        ["path"] = [.. Formattribute, "d", "stroke-linecap", "stroke-linejoin", "stroke-dasharray"],
        ["text"] = [.. Formattribute, "x", "y", "font-size", "font-weight", "font-family", "text-anchor"]
    };

    // Nur diese Pfadbefehle schreiben unsere Bauer (absolut). Ein Bogen (A) oder ein relativer
    // Befehl waere neu und muss bewusst geprueft werden, statt still anders auszusehen.
    private static readonly char[] ErlaubtePfadbefehle = ['M', 'L', 'C', 'Q', 'Z'];

    /// <summary>Alle Elementnamen der Teilmenge (Waechter und Fehlermeldungen).</summary>
    public static IReadOnlyCollection<string> UnterstuetzteElemente => Erlaubt.Keys;

    /// <summary>Erlaubte Attribute je Element (Waechter und Fehlermeldungen).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> UnterstuetzteAttribute { get; } =
        Erlaubt.ToDictionary(e => e.Key, e => (IReadOnlyCollection<string>)e.Value, StringComparer.Ordinal);

    /// <summary>
    /// Prueft ein ganzes SVG gegen die Teilmenge, ohne zu zeichnen. Fuer Waechtertests, die
    /// ohne Oberflaeche laufen.
    /// </summary>
    public static void PruefeTeilmenge(string svg)
    {
        var wurzel = Wurzel(svg);
        PruefeBaum(wurzel);
    }

    /// <summary>
    /// Zeichnet das SVG. <paramref name="themenquelle"/> ist das Element, ueber das die
    /// Ressourcensuche der Farbtoken laeuft (in der Regel die Zeichenflaeche selbst).
    /// </summary>
    public static Canvas Zeichne(string svg, FrameworkElement themenquelle)
    {
        ArgumentNullException.ThrowIfNull(themenquelle);

        var wurzel = Wurzel(svg);
        PruefeElement(wurzel);

        var breite = SvgWert.Zahl(wurzel, "width", 0);
        var hoehe = SvgWert.Zahl(wurzel, "height", 0);
        var flaeche = new Canvas
        {
            Width = breite > 0 ? breite : double.NaN,
            Height = hoehe > 0 ? hoehe : double.NaN
        };

        var definitionen = new SvgTeilmengeDefinitionen(themenquelle);
        ZeichneKinder(wurzel, flaeche, definitionen, themenquelle, breite, hoehe);
        return flaeche;
    }

    /// <summary>Prueft Name und Attribute genau eines Elements.</summary>
    internal static void PruefeElement(XElement element)
    {
        var name = element.Name.LocalName;
        if (!Erlaubt.TryGetValue(name, out var attribute))
            throw new NotSupportedException($"SVG-Teilmenge: Element '{name}' wird nicht gezeichnet.");

        foreach (var attribut in element.Attributes())
        {
            if (attribut.IsNamespaceDeclaration)
                continue;

            var attributName = attribut.Name.LocalName;
            if (!attribute.Contains(attributName, StringComparer.Ordinal))
            {
                throw new NotSupportedException(
                    $"SVG-Teilmenge: Attribut '{attributName}' an Element '{name}' wird nicht gezeichnet.");
            }
        }
    }

    /// <summary>True, wenn eine Farbangabe fehlt oder ausdruecklich "none" ist.</summary>
    internal static bool IstOhneFarbe(string? farbe)
    {
        var wert = (farbe ?? string.Empty).Trim();
        return wert.Length == 0 || string.Equals(wert, "none", StringComparison.OrdinalIgnoreCase);
    }

    private static XElement Wurzel(string svg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svg);
        var wurzel = XDocument.Parse(svg).Root
                     ?? throw new NotSupportedException("SVG-Teilmenge: leeres Dokument.");
        if (wurzel.Name.LocalName != "svg")
            throw new NotSupportedException($"SVG-Teilmenge: Wurzel '{wurzel.Name.LocalName}' ist kein <svg>.");
        return wurzel;
    }

    private static void PruefeBaum(XElement element)
    {
        PruefeElement(element);
        if (element.Name.LocalName == "path")
            PruefePfad((string?)element.Attribute("d"));
        foreach (var kind in element.Elements())
            PruefeBaum(kind);
    }

    /// <summary>
    /// Prueft jeden Buchstaben eines Pfad-<c>d</c>-Attributs gegen <see cref="ErlaubtePfadbefehle"/>.
    /// Gross-/Kleinschreibung zaehlt bewusst mit: In SVG ist ein Grossbuchstabe absolut und ein
    /// Kleinbuchstabe relativ — unsere Bauer schreiben ausschliesslich absolut. Ein
    /// <c>char.ToUpperInvariant</c> vor dem Vergleich wuerde einen relativen Befehl (z.B. 'l')
    /// unbemerkt durchlassen, obwohl er anders gezeichnet wird als sein absolutes Gegenstueck.
    /// </summary>
    private static void PruefePfad(string? daten)
    {
        foreach (var zeichen in daten ?? string.Empty)
        {
            if (char.IsLetter(zeichen) && !ErlaubtePfadbefehle.Contains(zeichen))
                throw new NotSupportedException($"SVG-Teilmenge: Pfadbefehl '{zeichen}' wird nicht gezeichnet.");
        }
    }

    private static void ZeichneKinder(
        XElement eltern,
        Canvas flaeche,
        SvgTeilmengeDefinitionen definitionen,
        FrameworkElement quelle,
        double breite,
        double hoehe)
    {
        foreach (var kind in eltern.Elements())
        {
            PruefeElement(kind);
            if (kind.Name.LocalName == "defs")
            {
                definitionen.Lies(kind, breite, hoehe);
                continue;
            }

            var element = Erzeuge(kind, definitionen, quelle, breite, hoehe);
            if (element is null)
                continue;

            flaeche.Children.Add(element);
        }
    }

    private static FrameworkElement? Erzeuge(
        XElement knoten,
        SvgTeilmengeDefinitionen definitionen,
        FrameworkElement quelle,
        double breite,
        double hoehe)
    {
        switch (knoten.Name.LocalName)
        {
            case "g":
                var gruppe = new Canvas { Width = breite, Height = hoehe };
                ZeichneKinder(knoten, gruppe, definitionen, quelle, breite, hoehe);
                Canvas.SetLeft(gruppe, 0);
                Canvas.SetTop(gruppe, 0);
                gruppe.RenderTransform = SvgWert.Transform((string?)knoten.Attribute("transform")) ?? Transform.Identity;
                return gruppe;

            case "rect":
                return Rechteck(knoten, definitionen, breite, hoehe);
            case "line":
                return Strich(knoten, definitionen);
            case "circle":
                return Kreis(knoten, definitionen);
            case "ellipse":
                return Ellipsenform(knoten, definitionen);
            case "polygon":
            case "polyline":
                return Vieleck(knoten, definitionen);
            case "path":
                return Pfad(knoten, definitionen);
            case "text":
                return Beschriftung(knoten, definitionen, quelle);
            default:
                throw new NotSupportedException(
                    $"SVG-Teilmenge: Element '{knoten.Name.LocalName}' wird nicht gezeichnet.");
        }
    }

    private static FrameworkElement Rechteck(XElement knoten, SvgTeilmengeDefinitionen definitionen, double breite, double hoehe)
    {
        var form = new Rectangle
        {
            Width = Math.Max(0d, SvgWert.Zahl(knoten, "width", 0, breite)),
            Height = Math.Max(0d, SvgWert.Zahl(knoten, "height", 0, hoehe)),
            RadiusX = SvgWert.Zahl(knoten, "rx", 0),
            RadiusY = SvgWert.Zahl(knoten, "ry", SvgWert.Zahl(knoten, "rx", 0))
        };
        Setze(form, knoten, definitionen, SvgWert.Zahl(knoten, "x", 0, breite), SvgWert.Zahl(knoten, "y", 0, hoehe));
        return form;
    }

    private static FrameworkElement Strich(XElement knoten, SvgTeilmengeDefinitionen definitionen)
    {
        var form = new Line
        {
            X1 = SvgWert.Zahl(knoten, "x1", 0),
            Y1 = SvgWert.Zahl(knoten, "y1", 0),
            X2 = SvgWert.Zahl(knoten, "x2", 0),
            Y2 = SvgWert.Zahl(knoten, "y2", 0)
        };
        Setze(form, knoten, definitionen, 0, 0);
        return form;
    }

    private static FrameworkElement Kreis(XElement knoten, SvgTeilmengeDefinitionen definitionen)
    {
        var r = Math.Max(0d, SvgWert.Zahl(knoten, "r", 0));
        var form = new Ellipse { Width = r * 2d, Height = r * 2d };
        Setze(form, knoten, definitionen, SvgWert.Zahl(knoten, "cx", 0) - r, SvgWert.Zahl(knoten, "cy", 0) - r);
        return form;
    }

    private static FrameworkElement Ellipsenform(XElement knoten, SvgTeilmengeDefinitionen definitionen)
    {
        var rx = Math.Max(0d, SvgWert.Zahl(knoten, "rx", 0));
        var ry = Math.Max(0d, SvgWert.Zahl(knoten, "ry", 0));
        var form = new Ellipse { Width = rx * 2d, Height = ry * 2d };
        Setze(form, knoten, definitionen, SvgWert.Zahl(knoten, "cx", 0) - rx, SvgWert.Zahl(knoten, "cy", 0) - ry);
        return form;
    }

    private static FrameworkElement Vieleck(XElement knoten, SvgTeilmengeDefinitionen definitionen)
    {
        var punkte = SvgWert.Punkte((string?)knoten.Attribute("points"));
        Shape form = knoten.Name.LocalName == "polygon"
            ? new Polygon { Points = punkte }
            : new Polyline { Points = punkte };
        Setze(form, knoten, definitionen, 0, 0);
        return form;
    }

    private static FrameworkElement Pfad(XElement knoten, SvgTeilmengeDefinitionen definitionen)
    {
        var daten = (string?)knoten.Attribute("d") ?? string.Empty;
        PruefePfad(daten);
        // Die WPF-Pfadsprache deckt M/L/C/Q/Z zeichengleich ab; geprueft wird trotzdem selbst,
        // damit ein neuer Befehl im Bauer nicht still anders aussieht.
        var form = new Path { Data = Geometry.Parse(daten) };
        Setze(form, knoten, definitionen, 0, 0);
        return form;
    }

    private static FrameworkElement Beschriftung(XElement knoten, SvgTeilmengeDefinitionen definitionen, FrameworkElement quelle)
    {
        var block = new TextBlock { Text = knoten.Value };

        var groesse = SvgWert.Zahl(knoten, "font-size", 0);
        if (groesse > 0)
            block.FontSize = groesse;

        var staerke = ((string?)knoten.Attribute("font-weight") ?? string.Empty).Trim();
        if (staerke.Length > 0)
        {
            block.FontWeight = staerke switch
            {
                "bold" => FontWeights.Bold,
                "normal" => FontWeights.Normal,
                _ => int.TryParse(staerke, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zahl)
                    ? FontWeight.FromOpenTypeWeight(Math.Clamp(zahl, 1, 999))
                    : FontWeights.Normal
            };
        }

        // "monospace" ist der technische Wert (Code, Meterstand); "sans-serif" nimmt bewusst die
        // Programmschrift, damit die Grafik im Programm nicht fremd wirkt. Die Schrift wird
        // gesetzt statt vererbt, weil das Mass fuer die Textausrichtung schon hier gebraucht wird.
        if (string.Equals((string?)knoten.Attribute("font-family"), "monospace", StringComparison.OrdinalIgnoreCase))
        {
            if (quelle.TryFindResource("FontMono") is FontFamily mono)
                block.FontFamily = mono;
        }
        else
        {
            block.FontFamily = TextElement.GetFontFamily(quelle);
        }

        // Anders als bei Flaechen bekommt ein Text ohne Farbangabe bewusst die Textfarbe des
        // Themes: In SVG waere er schwarz, und schwarz auf dunklem Grund ist unlesbar.
        var fuellung = (string?)knoten.Attribute("fill");
        if (!string.Equals(fuellung?.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            SvgFarbZuordnung.Setze(block, TextBlock.ForegroundProperty, fuellung);

        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var x = SvgWert.Zahl(knoten, "x", 0);
        var y = SvgWert.Zahl(knoten, "y", 0);
        var anker = ((string?)knoten.Attribute("text-anchor") ?? "start").Trim();
        var links = anker switch
        {
            "middle" => x - block.DesiredSize.Width / 2d,
            "end" => x - block.DesiredSize.Width,
            _ => x
        };
        // SVG setzt die Grundlinie, WPF die Oberkante.
        var oben = y - block.BaselineOffset;

        Canvas.SetLeft(block, links);
        Canvas.SetTop(block, oben);
        Deckung(block, knoten);
        Zuschneiden(block, knoten, definitionen, links, oben);
        Drehen(block, knoten, links, oben);
        return block;
    }

    /// <summary>Gemeinsame Formattribute: Lage, Fuellung, Strich, Deckung, Zuschnitt, Effekt.</summary>
    private static void Setze(Shape form, XElement knoten, SvgTeilmengeDefinitionen definitionen, double links, double oben)
    {
        Canvas.SetLeft(form, links);
        Canvas.SetTop(form, oben);

        var fuellung = (string?)knoten.Attribute("fill");
        if (!IstOhneFarbe(fuellung))
        {
            if (Verweis(fuellung) is { } id)
                form.Fill = definitionen.Pinsel(id);
            else
                SvgFarbZuordnung.Setze(form, Shape.FillProperty, fuellung);
        }

        var strich = (string?)knoten.Attribute("stroke");
        var staerke = SvgWert.Zahl(knoten, "stroke-width", 1);
        if (!IstOhneFarbe(strich))
        {
            if (Verweis(strich) is { } id)
                form.Stroke = definitionen.Pinsel(id);
            else
                SvgFarbZuordnung.Setze(form, Shape.StrokeProperty, strich);
            form.StrokeThickness = staerke;
        }

        if (SvgWert.Strichmuster((string?)knoten.Attribute("stroke-dasharray"), staerke) is { } muster)
            form.StrokeDashArray = muster;

        var kappe = ((string?)knoten.Attribute("stroke-linecap") ?? string.Empty).Trim();
        if (kappe.Length > 0)
        {
            var art = kappe switch
            {
                "round" => PenLineCap.Round,
                "square" => PenLineCap.Square,
                _ => PenLineCap.Flat
            };
            form.StrokeStartLineCap = art;
            form.StrokeEndLineCap = art;
            form.StrokeDashCap = art;
        }

        var ecke = ((string?)knoten.Attribute("stroke-linejoin") ?? string.Empty).Trim();
        if (ecke.Length > 0)
        {
            form.StrokeLineJoin = ecke switch
            {
                "round" => PenLineJoin.Round,
                "bevel" => PenLineJoin.Bevel,
                _ => PenLineJoin.Miter
            };
        }

        if (Verweis((string?)knoten.Attribute("filter")) is { } filterId)
            form.Effect = definitionen.Effekt(filterId);

        Deckung(form, knoten);
        Zuschneiden(form, knoten, definitionen, links, oben);
        Drehen(form, knoten, links, oben);
    }

    private static void Deckung(FrameworkElement element, XElement knoten)
    {
        var deckung = (string?)knoten.Attribute("opacity");
        if (!string.IsNullOrWhiteSpace(deckung))
            element.Opacity = Math.Clamp(SvgWert.ZahlOderProzent(deckung, 1, 1), 0d, 1d);
    }

    private static void Zuschneiden(
        FrameworkElement element,
        XElement knoten,
        SvgTeilmengeDefinitionen definitionen,
        double links,
        double oben)
    {
        if (Verweis((string?)knoten.Attribute("clip-path")) is not { } id)
            return;
        if (definitionen.Zuschnitt(id) is not { } bereich)
            return;

        // Der Zuschnitt steht in Flaechenkoordinaten, die Clip-Geometrie im Element selbst.
        element.Clip = new RectangleGeometry(new Rect(
            bereich.X - links,
            bereich.Y - oben,
            bereich.Width,
            bereich.Height));
    }

    private static void Drehen(FrameworkElement element, XElement knoten, double links, double oben)
    {
        if (SvgWert.Transform((string?)knoten.Attribute("transform")) is not { } transform)
            return;

        // Ein Drehpunkt steht in Flaechenkoordinaten; die RenderTransform dreht um das Element.
        if (transform is RotateTransform drehung)
        {
            drehung.CenterX -= links;
            drehung.CenterY -= oben;
        }

        element.RenderTransform = transform;
    }

    private static string? Verweis(string? wert)
    {
        var text = (wert ?? string.Empty).Trim();
        if (!text.StartsWith("url(#", StringComparison.Ordinal) || !text.EndsWith(")", StringComparison.Ordinal))
            return null;
        return text[5..^1];
    }
}
