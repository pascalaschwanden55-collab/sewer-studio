using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Liest die Stammdaten-Objekte der Klassen "Kanal" und "Haltung" aus einer SIA405-XTF.
/// Ausschliesslich lesend; die Datei bleibt unveraendert.
///
/// Beide Klassen werden gebraucht: Nutzungsart, Standortname und Zustand haengen am
/// Kanal, Material und lichte Hoehe an der Haltung. Im Kantonsexport von Abwasser Uri
/// tragen alle 109871 Kanal-Objekte weder Material noch Lichte_Hoehe.
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

        foreach (var node in doc.Descendants())
        {
            // Exakt auf ".Kanal" bzw. ".Haltung" enden — sonst treffen auch
            // ".Kanalschaden" und ".Haltung_Text" zu.
            var klasse = node.Name.LocalName switch
            {
                var name when name.EndsWith(".Kanal", StringComparison.Ordinal) => "Kanal",
                var name when name.EndsWith(".Haltung", StringComparison.Ordinal) => "Haltung",
                _ => null
            };

            if (klasse is null)
                continue;

            var tid = (string?)node.Attribute("TID");
            if (string.IsNullOrWhiteSpace(tid))
                continue;

            var werte = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kind in node.Elements())
            {
                // Die Geometrie bleibt draussen: "Verlauf" traegt bei einer langen
                // Haltung tausende Koordinatenzeichen, wird nie als Feld gelesen und
                // wuerde den Speicher unnoetig fuellen.
                if (string.Equals(kind.Name.LocalName, "Verlauf", StringComparison.Ordinal))
                    continue;

                if (!werte.ContainsKey(kind.Name.LocalName))
                    werte[kind.Name.LocalName] = kind.Value;
            }

            werte.TryGetValue("Bezeichnung", out var bezeichnung);
            elemente.Add(new XtfStammdatenElement(tid!, bezeichnung ?? "", werte, klasse));
        }

        return elemente;
    }
}
