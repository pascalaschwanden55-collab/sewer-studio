using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest die Antwort des Parzellendienstes. Rein: Text rein, Datensaetze raus.
/// Unlesbares ergibt eine leere Liste — der Aufrufer meldet dann "nicht gefunden",
/// statt mit geratenen Werten weiterzumachen.
/// </summary>
public static class ParcelWfsXmlParser
{
    public static IReadOnlyList<ParcelInfo> Parse(string? xml)
    {
        var wurzel = WfsGml.TryParse(xml);
        if (wurzel is null)
            return Array.Empty<ParcelInfo>();

        var ergebnis = new List<ParcelInfo>();

        foreach (var element in wurzel.Descendants()
                     .Where(e => e.Name.LocalName == "ch059_liegenschaften_flaechen"))
        {
            var nummer = WfsGml.Text(element, "nummer");
            if (nummer.Length == 0)
                continue;

            ergebnis.Add(new ParcelInfo(
                nummer,
                WfsGml.Int(element, "bfsnr") ?? 0,
                WfsGml.Text(element, "gemeinde"),
                WfsGml.Int(element, "flaechenmass"),
                WfsGml.Text(element, "egris_egrid"),
                WfsGml.PolygonWkt(element),
                WfsGml.Text(element, "url_grundbuch")));
        }

        return ergebnis;
    }

    /// <summary>Die Gemeindeliste kommt aus einer eigenen Ebene mit denselben Feldnamen.</summary>
    public static IReadOnlyList<Municipality> ParseMunicipalities(string? xml)
    {
        var wurzel = WfsGml.TryParse(xml);
        if (wurzel is null)
            return Array.Empty<Municipality>();

        var ergebnis = new List<Municipality>();

        foreach (var element in wurzel.Descendants()
                     .Where(e => e.Name.LocalName == "ch062_hoheitsgrenzen_gemeindegrenzen"))
        {
            var bfs = WfsGml.Int(element, "bfsnr");
            var name = WfsGml.Text(element, "gemeinde");
            if (bfs is null || name.Length == 0)
                continue;

            if (!ergebnis.Any(g => g.BfsNr == bfs.Value))
                ergebnis.Add(new Municipality(bfs.Value, name));
        }

        return ergebnis.OrderBy(g => g.Name, StringComparer.CurrentCulture).ToList();
    }
}
