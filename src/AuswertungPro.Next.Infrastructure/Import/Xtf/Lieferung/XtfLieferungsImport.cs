using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf.Lieferung;
using static AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung.XtfLieferungsDatenbank;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung;

internal static class XtfLieferungsImport
{
    internal static void Importiere(string quelle, string ziel, IProgress<string>? fortschritt, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); quelle = Path.GetFullPath(quelle); ziel = Path.GetFullPath(ziel);
        if (File.Exists(ziel) || string.Equals(quelle, ziel, StringComparison.OrdinalIgnoreCase)) throw new IOException("Eine vorhandene Datei wird nicht überschrieben. Bitte einen neuen Arbeitsdateinamen wählen.");
        var ordner = Path.GetDirectoryName(ziel)!; Directory.CreateDirectory(ordner);
        var temporaer = Path.Combine(ordner, ".lieferung-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (File.Open(temporaer, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            using (var input = new FileStream(quelle, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var c = Verbinde(temporaer, schreiben: true, neu: true))
            {
                Anlegen(c);
                using var tx = c.BeginTransaction();
                SetzeMeta(c, "quelle", quelle);
                using var insert = Befehl(c, "INSERT INTO objekte(korb,klasse,tid,name,original,namensschluessel) VALUES($korb,$klasse,$tid,$name,$xml,$nkey)",
                    ("$korb", 0), ("$klasse", ""), ("$tid", ""), ("$name", ""), ("$xml", ""), ("$nkey", ""));
                insert.Prepare();
                using (var r = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false }))
                {
                    r.MoveToContent();
                    if (r.LocalName != "TRANSFER" || r.NamespaceURI != XtfLieferungsXml.Ns.NamespaceName) throw new InvalidDataException("Bitte eine INTERLIS-2.3-Lieferung wählen.");
                    SetzeMeta(c, "transfer", Huelle(r).ToString(SaveOptions.DisableFormatting));
                    var imDatenbereich = false; long korb = 0, anzahl = 0; var header = false;
                    while (!r.EOF)
                    {
                        token.ThrowIfCancellationRequested();
                        if (r.NodeType == XmlNodeType.Element && r.Depth == 1 && r.LocalName == "HEADERSECTION")
                        {
                            if (header) throw new InvalidDataException("Mehr als ein XTF-Kopf gefunden.");
                            SetzeMeta(c, "header", ((XElement)XNode.ReadFrom(r)).ToString(SaveOptions.DisableFormatting)); header = true; continue;
                        }
                        if (r.Depth == 1 && r.LocalName == "DATASECTION") imDatenbereich = r.NodeType == XmlNodeType.Element;
                        else if (imDatenbereich && r.NodeType == XmlNodeType.Element && r.Depth == 2)
                        {
                            korb++;
                            Ausfuehren(c, "INSERT INTO koerbe(id,xml) VALUES($id,$xml)", ("$id", korb), ("$xml", Huelle(r).ToString(SaveOptions.DisableFormatting)));
                        }
                        else if (imDatenbereich && r.NodeType == XmlNodeType.Element && r.Depth == 3)
                        {
                            var e = (XElement)XNode.ReadFrom(r); var q = XtfLieferungsXml.Beleg(e);
                            insert.Parameters["$korb"].Value = korb; insert.Parameters["$klasse"].Value = q.Klasse;
                            insert.Parameters["$tid"].Value = q.Kennung.Length == 0 ? DBNull.Value : q.Kennung;
                            insert.Parameters["$name"].Value = q.Werte.GetValueOrDefault("Bezeichnung") ?? q.Werte.GetValueOrDefault("Textinhalt", "");
                            insert.Parameters["$xml"].Value = e.ToString(SaveOptions.DisableFormatting);
                            insert.Parameters["$nkey"].Value = (object?)XtfLieferungsNorm.Namensschluessel(q) ?? DBNull.Value;
                            insert.ExecuteNonQuery(); anzahl++;
                            if (anzahl % 5000 == 0) fortschritt?.Report($"{anzahl:N0} Objekte übernommen …");
                            continue;
                        }
                        r.Read();
                    }
                    if (!header || korb == 0 || anzahl == 0) throw new InvalidDataException("Die Datei enthält keine vollständige XTF-Lieferung.");
                }
                token.ThrowIfCancellationRequested(); fortschritt?.Report("Suchverzeichnis und Originalprüfsumme erstellen …");
                Indizes(c); input.Position = 0;
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var puffer = new byte[128 * 1024]; int count;
                while ((count = input.Read(puffer)) > 0) { token.ThrowIfCancellationRequested(); sha.AppendData(puffer, 0, count); }
                SetzeMeta(c, "sha256", Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant());
                SetzeMeta(c, "fertig", "1"); tx.Commit();
            }
            token.ThrowIfCancellationRequested(); File.Move(temporaer, ziel, overwrite: false);
        }
        finally
        {
            // Nur der selbst erzeugte, feste temporäre Dateiname wird entfernt, niemals ein Kundenpfad.
            if (File.Exists(temporaer)) File.Delete(temporaer);
        }
    }

    private static XElement Huelle(XmlReader r)
    {
        var e = new XElement(XName.Get(r.LocalName, r.NamespaceURI));
        if (r.MoveToFirstAttribute())
        {
            do { e.Add(new XAttribute(r.Name == "xmlns" ? XName.Get("xmlns") : XName.Get(r.LocalName, r.NamespaceURI), r.Value)); } while (r.MoveToNextAttribute());
            r.MoveToElement();
        }
        return e;
    }
}
