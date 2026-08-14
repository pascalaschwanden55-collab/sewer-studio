using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Liest die Stammdaten-Objekte der Klasse "Kanal" aus einer SIA405-XTF.
/// Ausschliesslich lesend; die Datei bleibt unveraendert.
/// </summary>
public static class XtfStammdatenElementReader
{
    public static IReadOnlyList<XtfStammdatenElement> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Array.Empty<XtfStammdatenElement>();

        return Parse(XDocument.Load(path));
    }

    /// <summary>
    /// Der Modellname aus dem Kopf der Uebertragung, etwa "SIA405_ABWASSER_2015_LV95".
    /// Er entscheidet ueber die gueltige Schreibweise mancher Werte. Gibt es mehrere
    /// Modelle, gilt das erste; fehlt der Kopf, ist das Ergebnis <c>null</c>.
    /// </summary>
    public static string? ReadModelName(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        return ParseModelName(XDocument.Load(path));
    }

    public static string? ParseModelName(XDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        return doc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "MODEL", StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("NAME"))
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
    }

    public static IReadOnlyList<XtfStammdatenElement> Parse(XDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var elemente = new List<XtfStammdatenElement>();

        // Exakt auf ".Kanal" enden — sonst wuerde auch ".Kanalschaden" zutreffen.
        foreach (var node in doc.Descendants()
                     .Where(e => e.Name.LocalName.EndsWith(".Kanal", StringComparison.Ordinal)))
        {
            var tid = (string?)node.Attribute("TID");
            if (string.IsNullOrWhiteSpace(tid))
                continue;

            var werte = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kind in node.Elements())
            {
                if (!werte.ContainsKey(kind.Name.LocalName))
                    werte[kind.Name.LocalName] = kind.Value;
            }

            werte.TryGetValue("Bezeichnung", out var bezeichnung);
            elemente.Add(new XtfStammdatenElement(tid!, bezeichnung ?? "", werte));
        }

        return elemente;
    }
}
