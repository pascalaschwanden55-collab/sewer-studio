using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Liest die Kanalschaden-Elemente einer VSA-KEK-XTF fuer die Zuordnung.
///
/// Ausschliesslich lesend: Die Datei wird geoeffnet, gelesen und wieder geschlossen.
/// Es wird nichts geschrieben, nichts verschoben und nichts archiviert — der
/// Kundenbestand bleibt unberuehrt.
/// </summary>
public static class XtfKanalschadenElementReader
{
    public static IReadOnlyList<XtfKanalschadenElement> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Array.Empty<XtfKanalschadenElement>();

        return Parse(XDocument.Load(path));
    }

    public static IReadOnlyList<XtfKanalschadenElement> Parse(XDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        // Haltungsbezeichnung je Untersuchung, damit der Altbestand ohne Anker
        // ueber den Haltungsnamen zugeordnet werden kann.
        var bezeichnungProUntersuchung = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in doc.Descendants()
                     .Where(e => e.Name.LocalName.EndsWith("Untersuchung", StringComparison.OrdinalIgnoreCase)))
        {
            var tid = (string?)node.Attribute("TID");
            if (string.IsNullOrWhiteSpace(tid))
                continue;

            bezeichnungProUntersuchung[tid!] = Kindwert(node, "Bezeichnung") ?? "";
        }

        var elemente = new List<XtfKanalschadenElement>();
        foreach (var node in doc.Descendants()
                     .Where(e => e.Name.LocalName.EndsWith("Kanalschaden", StringComparison.OrdinalIgnoreCase)))
        {
            var tid = (string?)node.Attribute("TID");
            if (string.IsNullOrWhiteSpace(tid))
                continue;

            var refNode = node.Elements()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, "UntersuchungRef", StringComparison.Ordinal));
            var untersuchungTid = (string?)refNode?.Attribute("REF") ?? "";

            bezeichnungProUntersuchung.TryGetValue(untersuchungTid, out var bezeichnung);

            elemente.Add(new XtfKanalschadenElement(
                KanalschadenTid: tid!,
                UntersuchungTid: untersuchungTid,
                HaltungBezeichnung: bezeichnung ?? "",
                Code: Kindwert(node, "KanalSchadencode") ?? "",
                Distanz: XtfValueNormalizer.TryParseDouble(Kindwert(node, "Distanz"), out var meter) ? meter : null,
                Videozaehlerstand: Kindwert(node, "Videozaehlerstand"),
                Quantifizierung1: Kindwert(node, "Quantifizierung1"),
                Quantifizierung2: Kindwert(node, "Quantifizierung2")));
        }

        return elemente;
    }

    private static string? Kindwert(XElement node, string localName)
        => node.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.Ordinal))
            ?.Value;
}
