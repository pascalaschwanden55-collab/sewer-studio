using System.Xml;
using AuswertungPro.Next.Application.Export.Geonis;

namespace AuswertungPro.Next.Infrastructure.Export.Geonis;

/// <summary>
/// Zweiter Lesedurchgang: holt den unveraenderten XML-Quelltext der wenigen Objekte, die der
/// Rueckschrieb wirklich betrifft.
///
/// Warum das ganze Objekt und nicht nur die geaenderten Attribute: Eine INTERLIS-Datei mit
/// halben Objekten ist nicht modellgueltig (Pflichtattribute fehlen). Wir liefern darum das
/// vollstaendige Objekt aus dem Kataster und aendern darin nur die beurteilten Werte.
/// </summary>
public sealed class Sia405ObjektQuelltextLeser : ISia405ObjektQuelltextLeser
{
    public IReadOnlyDictionary<string, string> Lies(string katasterXtfPfad, IReadOnlyCollection<string> tids)
    {
        ArgumentNullException.ThrowIfNull(tids);

        if (string.IsNullOrWhiteSpace(katasterXtfPfad))
            throw new ArgumentException("Pfad zur Kataster-XTF fehlt.", nameof(katasterXtfPfad));
        if (!File.Exists(katasterXtfPfad))
            throw new FileNotFoundException($"Kataster-XTF nicht gefunden: {katasterXtfPfad}", katasterXtfPfad);

        var ergebnis = new Dictionary<string, string>(StringComparer.Ordinal);
        var gesucht = new HashSet<string>(tids, StringComparer.Ordinal);
        if (gesucht.Count == 0)
            return ergebnis;

        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(katasterXtfPfad, settings);

        // ReadOuterXml bewegt den Reader bereits weiter — skipRead verhindert ein zweites Read().
        var skipRead = false;
        while (skipRead || reader.Read())
        {
            skipRead = false;

            if (reader.NodeType != XmlNodeType.Element)
                continue;

            var tid = reader.GetAttribute("TID");
            if (tid is null || !gesucht.Contains(tid) || ergebnis.ContainsKey(tid))
                continue;

            ergebnis[tid] = reader.ReadOuterXml();
            skipRead = true;

            if (ergebnis.Count == gesucht.Count)
                break;
        }

        return ergebnis;
    }
}
