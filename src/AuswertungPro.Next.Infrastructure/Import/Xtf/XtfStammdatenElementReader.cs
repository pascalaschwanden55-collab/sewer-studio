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
