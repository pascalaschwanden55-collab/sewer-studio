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
                WfsGml.LineStringWkt(element))
            {
                // Additiv: Der Dossier-Weg nutzt diese Angaben nicht, der
                // Feld-Nachschlag fuellt damit sonst leere Projektfelder.
                // FunktionHierarchisch etwa ist in 473 von 475 Haltungen leer.
                FunktionHierarchisch = LeerAlsNull(WfsGml.Text(element, "ka_funktionhierarchisch")),
                NutzungsartIst = LeerAlsNull(WfsGml.Text(element, "ka_nutzungsart_ist")),
                Material = LeerAlsNull(WfsGml.Text(element, "ha_material")),
                LichteHoehe = LeerAlsNull(WfsGml.Text(element, "ha_lichte_hoehe")),
                Status = LeerAlsNull(WfsGml.Text(element, "bw_status")),
            });
        }

        return ergebnis;
    }

    private static string? LeerAlsNull(string? wert)
        => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
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
        var flaechen = new List<string>();

        foreach (var polygon in element.Descendants()
                     .Where(e => e.Name.LocalName == "Polygon"))
        {
            var ringe = new List<string>();

            foreach (var rand in polygon.Elements()
                         .Where(e => e.Name.LocalName is "exterior" or "interior"))
            {
                var punkte = Punkte(
                    rand.Descendants().FirstOrDefault(e => e.Name.LocalName == "posList")?.Value,
                    mindestPunkte: 4);

                if (punkte.Count == 0)
                    return string.Empty;

                ringe.Add(string.Join(",", punkte));
            }

            if (ringe.Count == 0)
                return string.Empty;

            // Der erste Ring ist die Aussenkante, jeder weitere ein Loch — meist
            // eine umschlossene Nachbarparzelle. Wuerde man sie gleichrangig
            // behandeln, faende die raeumliche Suche auch die Leitungen des
            // Nachbarn und legte sie ins falsche Dossier.
            flaechen.Add("((" + string.Join("),(", ringe) + "))");
        }

        if (flaechen.Count == 0)
            return string.Empty;

        return flaechen.Count == 1
            ? "POLYGON" + flaechen[0]
            : "MULTIPOLYGON(" + string.Join(",", flaechen) + ")";
    }

    public static string LineStringWkt(XElement element)
    {
        var teile = Geometrieteile(element, mindestPunkte: 2);
        if (teile.Count == 0)
            return string.Empty;

        return teile.Count == 1
            ? "LINESTRING(" + teile[0] + ")"
            : "MULTILINESTRING((" + string.Join("),(", teile) + "))";
    }

    /// <summary>
    /// Alle Teile einer Geometrie. Eine Parzelle kann aus mehreren getrennten
    /// Flaechen bestehen, eine Haltung aus mehreren Linienstuecken — nur den
    /// ersten Teil zu lesen ergaebe einen halben Umriss, und die raeumliche
    /// Suche wuerde still zu wenige Leitungen finden.
    ///
    /// Ist auch nur ein Teil unlesbar, gilt die ganze Geometrie als unlesbar:
    /// eine halbe Flaeche ist schlimmer als gar keine.
    /// </summary>
    private static List<string> Geometrieteile(XElement element, int mindestPunkte)
    {
        var ergebnis = new List<string>();

        foreach (var posList in element.Descendants()
                     .Where(e => e.Name.LocalName == "posList"))
        {
            var punkte = Punkte(posList.Value, mindestPunkte);
            if (punkte.Count == 0)
                return new List<string>();

            ergebnis.Add(string.Join(",", punkte));
        }

        return ergebnis;
    }

    /// <summary>
    /// GML gibt die Koordinaten als flache Zahlenfolge "x y x y ...". Eine
    /// ungerade Anzahl waere unvollstaendig, und jedes Token muss wirklich eine
    /// Zahl sein — sonst entstuende aus Datenmuell eine gueltig aussehende
    /// Geometrie.
    /// </summary>
    private static List<string> Punkte(string? posList, int mindestPunkte)
    {
        if (string.IsNullOrWhiteSpace(posList))
            return new List<string>();

        var zahlen = posList.Split(
            new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (zahlen.Length % 2 != 0 || zahlen.Length / 2 < mindestPunkte)
            return new List<string>();

        foreach (var zahl in zahlen)
        {
            if (!double.TryParse(zahl, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return new List<string>();
        }

        var punkte = new List<string>(zahlen.Length / 2);
        for (var i = 0; i < zahlen.Length; i += 2)
            punkte.Add(zahlen[i] + " " + zahlen[i + 1]);

        return punkte;
    }
}
