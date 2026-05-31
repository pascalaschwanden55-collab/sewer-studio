using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Liest SIA405-XTF und liefert je Schacht (Abwasserknoten) einen Punkt (LV95).
/// Streaming via XmlReader, damit grosse Dateien (~700 MB) nicht in den RAM muessen.
/// Geometrie liegt am Abwasserknoten unter Lage/COORD/C1,C2.
/// </summary>
public sealed class XtfManholeExtractor
{
    public IEnumerable<ManholeGeometry> Extract(string xtfPath)
    {
        var settings = new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true };
        using var reader = XmlReader.Create(xtfPath, settings);

        string? name = null;
        double? x = null;
        double? y = null;
        bool inKnoten = false;

        // ReadElementContentAsString() bewegt den Reader bereits weiter —
        // skipRead verhindert ein zweites reader.Read() danach.
        bool skipRead = false;

        while (skipRead || reader.Read())
        {
            skipRead = false;

            if (reader.NodeType == XmlNodeType.Element)
            {
                var local = reader.LocalName;
                if (local.EndsWith(".Abwasserknoten", StringComparison.Ordinal))
                {
                    inKnoten = true; name = null; x = null; y = null;
                }
                else if (inKnoten && local == "Bezeichnung" && name == null)
                {
                    name = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inKnoten && local == "C1" && x == null)
                {
                    var raw = reader.ReadElementContentAsString();
                    skipRead = true;
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        x = v;
                }
                else if (inKnoten && local == "C2" && y == null)
                {
                    var raw = reader.ReadElementContentAsString();
                    skipRead = true;
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        y = v;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (reader.LocalName.EndsWith(".Abwasserknoten", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(name) && x.HasValue && y.HasValue)
                        yield return new ManholeGeometry(name!, x.Value, y.Value);
                    inKnoten = false; name = null; x = null; y = null;
                }
            }
        }
    }
}
