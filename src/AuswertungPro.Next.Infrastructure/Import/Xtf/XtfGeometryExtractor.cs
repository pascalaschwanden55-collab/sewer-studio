using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Geometry;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Extrahiert Liniengeometrien (Haltungen) und Punktgeometrien (Schaechte)
/// aus SIA-405-XTF-Dateien. Koordinaten werden als LV95 (EPSG:2056) gelesen.
///
/// Phase 1 (Geometrie-Fundament 2026-05-23).
/// </summary>
public static class XtfGeometryExtractor
{
    /// <summary>
    /// Liest alle Haltungen mit Verlauf aus der XTF-Datei.
    /// Key = Bezeichnung der Haltung.
    /// Haltungen ohne &lt;Verlauf&gt; tauchen NICHT im Ergebnis auf.
    /// </summary>
    public static IReadOnlyDictionary<string, HaltungGeometrie> ExtractHaltungen(string xtfPath)
    {
        var result = new Dictionary<string, HaltungGeometrie>(StringComparer.OrdinalIgnoreCase);
        if (!TryLoadXtf(xtfPath, out var doc) || doc is null) return result;

        foreach (var haltung in doc.Descendants().Where(e => HasLocalName(e, "Haltung")))
        {
            var bezeichnung = ChildValue(haltung, "Bezeichnung");
            if (string.IsNullOrWhiteSpace(bezeichnung)) continue;

            var verlauf = haltung.Elements().FirstOrDefault(c => HasLocalName(c, "Verlauf"));
            if (verlauf is null) continue;

            var punkte = ParseCoordSequence(verlauf);
            if (punkte.Count < 2) continue;

            result[bezeichnung!] = new HaltungGeometrie
            {
                Verlauf = punkte,
                Source = GeometrySource.Xtf,
            };
        }

        return result;
    }

    /// <summary>
    /// Liest alle Schacht-Lagen aus der XTF-Datei.
    /// Key = Bezeichnung des Abwasserknotens.
    /// Knoten ohne &lt;Lage&gt; tauchen NICHT im Ergebnis auf.
    /// </summary>
    public static IReadOnlyDictionary<string, SchachtLage> ExtractSchachtLagen(string xtfPath)
    {
        var result = new Dictionary<string, SchachtLage>(StringComparer.OrdinalIgnoreCase);
        if (!TryLoadXtf(xtfPath, out var doc) || doc is null) return result;

        foreach (var knoten in doc.Descendants().Where(e => HasLocalName(e, "Abwasserknoten")))
        {
            var bezeichnung = ChildValue(knoten, "Bezeichnung");
            if (string.IsNullOrWhiteSpace(bezeichnung)) continue;

            var lage = knoten.Elements().FirstOrDefault(c => HasLocalName(c, "Lage"));
            if (lage is null) continue;

            var punkte = ParseCoordSequence(lage);
            if (punkte.Count == 0) continue;

            result[bezeichnung!] = new SchachtLage
            {
                Punkt = punkte[0],
                Source = GeometrySource.Xtf,
            };
        }

        return result;
    }

    // --- Helpers --------------------------------------------------------

    // DTD prohibited + XmlResolver=null schuetzt vor XXE-Attacken bei
    // XTF-Quellen aus fremden Tools (Goldstandard fuer System.Xml-Loader).
    private static readonly XmlReaderSettings _xmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    private static bool TryLoadXtf(string xtfPath, out XDocument? doc)
    {
        doc = null;
        if (string.IsNullOrWhiteSpace(xtfPath) || !File.Exists(xtfPath))
            return false;

        try
        {
            using var stream = File.OpenRead(xtfPath);
            using var reader = XmlReader.Create(stream, _xmlSettings);
            doc = XDocument.Load(reader);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// SIA-405-XTF nutzt qualifizierte Element-Namen wie
    /// "SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung".
    /// Wir matchen daher auf LocalName == name ODER LocalName endet
    /// mit "." + name (konsistent mit XtfStammdatenExtractor).
    /// </summary>
    private static bool HasLocalName(XElement e, string name)
        => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)
        || e.Name.LocalName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase);

    private static string? ChildValue(XElement parent, string localName)
        => parent.Elements()
                 .FirstOrDefault(c => HasLocalName(c, localName))
                 ?.Value?.Trim();

    /// <summary>
    /// Liest alle C1/C2-Werte im Teilbaum unter <paramref name="container"/>
    /// und kombiniert sie paarweise zu LV95-Koordinaten in Lese-Reihenfolge.
    /// Funktioniert sowohl fuer kompakte Notation (mehrere C1/C2 in einem
    /// einzelnen &lt;COORD&gt;) als auch fuer normgerechte Notation
    /// (ein &lt;COORD&gt; pro Punkt).
    /// </summary>
    private static List<Lv95Coordinate> ParseCoordSequence(XElement container)
    {
        var c1s = new List<double>();
        var c2s = new List<double>();

        foreach (var leaf in container.Descendants())
        {
            if (HasLocalName(leaf, "C1") && TryParseDouble(leaf.Value, out var v1))
                c1s.Add(v1);
            else if (HasLocalName(leaf, "C2") && TryParseDouble(leaf.Value, out var v2))
                c2s.Add(v2);
        }

        var n = Math.Min(c1s.Count, c2s.Count);
        var punkte = new List<Lv95Coordinate>(n);
        for (var i = 0; i < n; i++)
            punkte.Add(new Lv95Coordinate(c1s[i], c2s[i]));
        return punkte;
    }

    private static bool TryParseDouble(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return double.TryParse(
            raw.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }
}
