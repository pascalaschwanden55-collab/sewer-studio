using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Liest SIA405-XTF und liefert je Haltung die Verlauf-Polylinie (LV95).
/// Streaming via XmlReader, damit grosse Dateien (~600 MB) nicht in den RAM muessen.
/// </summary>
public sealed class XtfNetworkExtractor
{
    public IEnumerable<HaltungGeometry> Extract(string xtfPath)
    {
        var settings = new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true };
        using var reader = XmlReader.Create(xtfPath, settings);

        string? name = null;
        List<(double X, double Y)>? points = null;
        bool inVerlauf = false;
        double? c1 = null;

        // ReadElementContentAsString() bewegt den Reader bereits weiter —
        // skipRead verhindert, dass reader.Read() danach nochmals aufgerufen wird.
        bool skipRead = false;

        while (skipRead || reader.Read())
        {
            skipRead = false;

            if (reader.NodeType == XmlNodeType.Element)
            {
                var local = reader.LocalName;
                if (local.EndsWith(".Haltung", StringComparison.Ordinal))
                {
                    name = null; points = new(); inVerlauf = false; c1 = null;
                }
                else if (points != null && local == "Bezeichnung" && name == null)
                {
                    name = reader.ReadElementContentAsString();
                    // Reader steht jetzt auf dem naechsten Knoten — nicht nochmals Read() aufrufen.
                    skipRead = true;
                }
                else if (local == "Verlauf") inVerlauf = true;
                else if (inVerlauf && local == "C1")
                {
                    var raw = reader.ReadElementContentAsString();
                    skipRead = true;
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var c1Val))
                        c1 = c1Val;
                    else
                    {
                        // Fehlerhafte Koordinate — gesamte Haltung verwerfen
                        points = null;
                        c1 = null;
                    }
                }
                else if (inVerlauf && local == "C2" && c1.HasValue)
                {
                    var raw = reader.ReadElementContentAsString();
                    skipRead = true;
                    if (points != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var c2Val))
                    {
                        points.Add((c1.Value, c2Val));
                        c1 = null;
                    }
                    else
                        points = null; // Fehlerhafte Koordinate oder bereits verworfene Haltung
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (reader.LocalName == "Verlauf") inVerlauf = false;
                else if (reader.LocalName.EndsWith(".Haltung", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(name) && points is { Count: >= 2 })
                        yield return new HaltungGeometry(name!, points);
                    name = null; points = null;
                }
            }
        }
    }
}
