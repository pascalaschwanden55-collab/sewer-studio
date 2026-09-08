using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Kleine Lesehilfen fuer die SVG-Teilmenge: Zahlen, Prozentwerte, Punktlisten,
/// Strichmuster und die zwei erlaubten Transformationen. Alles invariant gelesen —
/// die Bauer schreiben ihre Zahlen mit Punkt als Dezimaltrenner.
/// </summary>
internal static class SvgWert
{
    /// <summary>Zahl eines Attributs; <paramref name="bezug"/> loest einen Prozentwert auf.</summary>
    public static double Zahl(XElement element, string name, double rueckfall, double bezug = 0)
        => ZahlOderProzent((string?)element.Attribute(name), rueckfall, bezug);

    /// <summary>Zahl aus einem Text; Prozent wird auf <paramref name="bezug"/> bezogen.</summary>
    public static double ZahlOderProzent(string? text, double rueckfall, double bezug = 0)
    {
        var wert = (text ?? string.Empty).Trim();
        if (wert.Length == 0)
            return rueckfall;

        if (wert.EndsWith("%", StringComparison.Ordinal))
        {
            return double.TryParse(wert[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var anteil)
                ? anteil / 100d * bezug
                : rueckfall;
        }

        return double.TryParse(wert, NumberStyles.Float, CultureInfo.InvariantCulture, out var zahl)
            ? zahl
            : rueckfall;
    }

    /// <summary>Punktliste eines <c>polygon</c>/<c>polyline</c> ("x,y x,y" oder "x y, x y").</summary>
    public static PointCollection Punkte(string? text)
    {
        var zahlen = Zerlege(text);
        var punkte = new PointCollection();
        for (var i = 0; i + 1 < zahlen.Count; i += 2)
            punkte.Add(new Point(zahlen[i], zahlen[i + 1]));
        return punkte;
    }

    /// <summary>
    /// Strichmuster. SVG misst in Zeichenflaeche-Einheiten, WPF in Vielfachen der Strichstaerke —
    /// ohne diese Umrechnung waeren die Striche bei Staerke 1,2 um 20 Prozent zu lang.
    /// </summary>
    public static DoubleCollection? Strichmuster(string? text, double staerke)
    {
        var zahlen = Zerlege(text);
        if (zahlen.Count == 0)
            return null;

        var teiler = staerke > 0 ? staerke : 1d;
        return new DoubleCollection(zahlen.Select(z => z / teiler));
    }

    /// <summary>
    /// <c>translate(x[,y])</c> und <c>rotate(winkel[,cx,cy])</c> — mehr schreiben unsere Bauer nicht.
    /// Alles andere ist ein Fehler, keine stille Nulltransformation.
    /// </summary>
    public static Transform? Transform(string? text)
    {
        var wert = (text ?? string.Empty).Trim();
        if (wert.Length == 0)
            return null;

        var klammer = wert.IndexOf('(');
        if (klammer <= 0 || !wert.EndsWith(")", StringComparison.Ordinal))
            throw new NotSupportedException($"SVG-Teilmenge: Transformation '{wert}' ist nicht vorgesehen.");

        var name = wert[..klammer].Trim();
        var zahlen = Zerlege(wert[(klammer + 1)..^1]);

        switch (name)
        {
            case "translate":
                return new TranslateTransform(
                    zahlen.Count > 0 ? zahlen[0] : 0,
                    zahlen.Count > 1 ? zahlen[1] : 0);
            case "rotate":
                var winkel = zahlen.Count > 0 ? zahlen[0] : 0;
                return zahlen.Count >= 3
                    ? new RotateTransform(winkel, zahlen[1], zahlen[2])
                    : new RotateTransform(winkel);
            default:
                throw new NotSupportedException($"SVG-Teilmenge: Transformation '{name}' ist nicht vorgesehen.");
        }
    }

    private static List<double> Zerlege(string? text)
    {
        var zahlen = new List<double>();
        foreach (var teil in (text ?? string.Empty).Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(teil, NumberStyles.Float, CultureInfo.InvariantCulture, out var zahl))
                zahlen.Add(zahl);
        }

        return zahlen;
    }
}
