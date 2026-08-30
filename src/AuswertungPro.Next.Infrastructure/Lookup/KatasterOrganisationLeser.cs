using System;
using System.Collections.Generic;
using System.Xml;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Liest die Betreiber-Organisationen aus einer Abwasserkataster-XTF.
///
/// Im heutigen Bestand des Kantons Uri gibt es genau eine ("Abwasser Uri"),
/// und alle Eigentuemer-Verweise zeigen darauf. Solange das so ist, laesst
/// sich der Eigentuemer ohne Aufloesung der Verweise benennen.
///
/// Sobald mehrere Organisationen auftauchen, ist diese Abkuerzung falsch:
/// Dann muesste je Objekt der EigentuemerRef aufgeloest werden. Der Leser
/// meldet in dem Fall bewusst nichts, statt den Namen des ersten Betreibers
/// auf alle Leitungen zu uebertragen.
/// </summary>
public static class KatasterOrganisationLeser
{
    private static readonly object Sperre = new();
    private static readonly Dictionary<string, string?> Zwischenspeicher =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Der Name der einzigen Organisation, oder null bei keiner, mehreren
    /// oder einem Lesefehler. Das Ergebnis wird je Datei gemerkt — die Datei
    /// ist mehrere hundert Megabyte gross.
    /// </summary>
    public static string? LiesEinzigeOrganisation(string xtfPfad)
    {
        if (string.IsNullOrWhiteSpace(xtfPfad))
            return null;

        lock (Sperre)
        {
            if (Zwischenspeicher.TryGetValue(xtfPfad, out var gemerkt))
                return gemerkt;
        }

        var name = Lies(xtfPfad);

        lock (Sperre)
        {
            Zwischenspeicher[xtfPfad] = name;
        }

        return name;
    }

    private static string? Lies(string xtfPfad)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(xtfPfad, settings);

            var namen = new List<string>();
            var inOrganisation = false;
            var skipRead = false;

            while (skipRead || reader.Read())
            {
                skipRead = false;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName.EndsWith(".Organisation", StringComparison.Ordinal))
                    {
                        inOrganisation = true;
                    }
                    else if (inOrganisation && reader.LocalName == "Bezeichnung")
                    {
                        var wert = reader.ReadElementContentAsString();
                        skipRead = true;
                        if (!string.IsNullOrWhiteSpace(wert))
                            namen.Add(wert.Trim());

                        // Mehr als eine: Die Abkuerzung traegt nicht mehr.
                        if (namen.Count > 1)
                            return null;
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement
                         && reader.LocalName.EndsWith(".Organisation", StringComparison.Ordinal))
                {
                    inOrganisation = false;
                }
            }

            return namen.Count == 1 ? namen[0] : null;
        }
        catch
        {
            return null;
        }
    }
}
