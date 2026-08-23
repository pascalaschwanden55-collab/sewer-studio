using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest die Antwort der Abwasser-Netzebene. Rein: Text rein, Datensaetze raus.
/// </summary>
public static class SewerNetworkWfsXmlParser
{
    public static IReadOnlyList<NetworkHolding> Parse(string? xml)
    {
        var wurzel = WfsGml.TryParse(xml);
        if (wurzel is null)
            return Array.Empty<NetworkHolding>();

        var ergebnis = new List<NetworkHolding>();

        foreach (var element in wurzel.Descendants()
                     .Where(e => e.Name.LocalName == "abw_haltungen"))
        {
            var bezeichnung = WfsGml.Text(element, "ne_bezeichnung");
            if (bezeichnung.Length == 0)
                continue;

            ergebnis.Add(new NetworkHolding(
                bezeichnung,
                WfsGml.Text(element, "org_eigentuemer"),
                WfsGml.Double(element, "ha_laengeeffektiv"),
                WfsGml.LineStringWkt(element)));
        }

        return ergebnis;
    }
}

/// <summary>
/// Die drei Handgriffe, die beide WFS-Parser brauchen. Bewusst intern und klein:
/// Feldnamen werden ohne Namensraum gesucht, weil der Dienst seine Praefixe
/// aendern kann, ohne dass sich die Feldnamen aendern.
/// </summary>
internal static class WfsGml
{
    public static XElement? TryParse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            return XDocument.Parse(xml).Root;
        }
        catch (System.Xml.XmlException)
        {
            // Der Dienst antwortet im Fehlerfall auch mal mit HTML.
            return null;
        }
    }

    public static string Text(XElement element, string feldname)
        => element.Descendants()
               .FirstOrDefault(e => e.Name.LocalName == feldname)?.Value.Trim()
           ?? string.Empty;

    public static int? Int(XElement element, string feldname)
        => int.TryParse(Text(element, feldname), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;

    public static double? Double(XElement element, string feldname)
        => double.TryParse(Text(element, feldname), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;

    public static string PolygonWkt(XElement element)
    {
        var punkte = Punkte(element);
        return punkte.Count == 0 ? string.Empty : "POLYGON((" + string.Join(",", punkte) + "))";
    }

    public static string LineStringWkt(XElement element)
    {
        var punkte = Punkte(element);
        return punkte.Count == 0 ? string.Empty : "LINESTRING(" + string.Join(",", punkte) + ")";
    }

    /// <summary>
    /// GML gibt die Koordinaten als flache Zahlenfolge "x y x y ...". Eine
    /// ungerade Anzahl waere unvollstaendig und ergibt gar nichts.
    /// </summary>
    private static List<string> Punkte(XElement element)
    {
        var posList = element.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "posList")?.Value;

        if (string.IsNullOrWhiteSpace(posList))
            return new List<string>();

        var zahlen = posList.Split(
            new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (zahlen.Length < 4 || zahlen.Length % 2 != 0)
            return new List<string>();

        var punkte = new List<string>(zahlen.Length / 2);
        for (var i = 0; i < zahlen.Length; i += 2)
            punkte.Add(zahlen[i] + " " + zahlen[i + 1]);

        return punkte;
    }
}
