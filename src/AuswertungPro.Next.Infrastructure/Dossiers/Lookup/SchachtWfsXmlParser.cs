using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest die Antwort der Schacht-Netzebene. Rein: Text rein, Datensaetze raus.
/// </summary>
public static class SchachtWfsXmlParser
{
    public static IReadOnlyList<NetworkSchacht> Parse(string? xml)
    {
        var wurzel = WfsGml.TryParse(xml);
        if (wurzel is null)
            return Array.Empty<NetworkSchacht>();

        var ergebnis = new List<NetworkSchacht>();

        foreach (var element in wurzel.Descendants()
                     .Where(e => e.Name.LocalName == "abw_normschaechte"))
        {
            var bezeichnung = WfsGml.Text(element, "bw_bezeichnung");
            if (bezeichnung.Length == 0)
                continue;

            ergebnis.Add(new NetworkSchacht(
                bezeichnung,
                WfsGml.Text(element, "org_eigentuemer"))
            {
                Funktion = LeerAlsNull(WfsGml.Text(element, "ns_funktion")),
                Material = LeerAlsNull(WfsGml.Text(element, "ns_material")),
                Nutzungsart = LeerAlsNull(WfsGml.Text(element, "ka_nutzungsart")),
                Status = LeerAlsNull(WfsGml.Text(element, "bw_status")),
            });
        }

        return ergebnis;
    }

    private static string? LeerAlsNull(string? wert)
        => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
