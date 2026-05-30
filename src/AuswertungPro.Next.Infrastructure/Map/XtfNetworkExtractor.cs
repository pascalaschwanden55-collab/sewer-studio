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
                    c1 = double.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
                    skipRead = true;
                }
                else if (inVerlauf && local == "C2" && c1.HasValue)
                {
                    var c2 = double.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
                    points!.Add((c1.Value, c2));
                    c1 = null;
                    skipRead = true;
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
