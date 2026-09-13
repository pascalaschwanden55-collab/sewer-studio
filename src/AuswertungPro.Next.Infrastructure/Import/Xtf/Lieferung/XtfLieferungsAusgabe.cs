using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using AuswertungPro.Next.Application.Xtf.Lieferung;
using static AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung.XtfLieferungsDatenbank;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung;

internal static class XtfLieferungsAusgabe
{
    internal static XtfLieferungsPruefung Ausfuehren(string datei, string? ziel, IProgress<string>? fortschritt, CancellationToken token)
    {
        if (ziel is not null && File.Exists(Path.GetFullPath(ziel))) throw new IOException("Die Zieldatei existiert bereits und wird nicht überschrieben.");
        using var c = Verbinde(datei, schreiben: true); using var tx = c.BeginTransaction();
        var pruefung = Pruefen(c, fortschritt, token);
        SetzeMeta(c, "pruefbericht", pruefung.Bericht);
        string? temporaer = null;
        try
        {
            if (ziel is not null && pruefung.FehlerhafteObjekte == 0)
            {
                ziel = Path.GetFullPath(ziel); var ordner = Path.GetDirectoryName(ziel)!; Directory.CreateDirectory(ordner);
                temporaer = Path.Combine(ordner, ".xtf-ausgabe-" + Guid.NewGuid().ToString("N") + ".tmp");
                fortschritt?.Report("Geprüfte Objekte in neue XTF schreiben …");
                Schreibe(c, temporaer, token);
            }
            token.ThrowIfCancellationRequested(); tx.Commit();
            if (temporaer is not null)
            {
                File.Move(temporaer, ziel!, overwrite: false);
                pruefung = pruefung with { Bericht = pruefung.Bericht + "\nNeue XTF: " + ziel };
            }
            return pruefung;
        }
        finally { if (temporaer is not null && File.Exists(temporaer)) File.Delete(temporaer); }
    }

    private static XtfLieferungsPruefung Pruefen(SqliteConnection c, IProgress<string>? fortschritt, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); fortschritt?.Report("Originalkennungen und Beziehungen prüfen …");
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var cmd = Befehl(c, "SELECT tid,MIN(klasse),COUNT(*) FROM objekte WHERE tid IS NOT NULL GROUP BY tid"))
        using (var r = cmd.ExecuteReader()) while (r.Read())
        { token.ThrowIfCancellationRequested(); ids.Add(r.GetString(0), r.GetInt64(2) == 1 ? r.GetString(1) : "doppelte Kennung"); }
        var doppelteNamen = new HashSet<string>(StringComparer.Ordinal);
        using (var cmd = Befehl(c, "SELECT namensschluessel FROM objekte WHERE namensschluessel IS NOT NULL GROUP BY namensschluessel HAVING COUNT(*)>1"))
        using (var r = cmd.ExecuteReader()) while (r.Read()) doppelteNamen.Add(r.GetString(0));
        XtfLieferungsDatenbank.Ausfuehren(c, "UPDATE objekte SET problem='' WHERE problem<>''");
        var details = new StringBuilder(); long anzahl = 0, fehler = 0; var externe = new HashSet<string>();
        using var update = Befehl(c, "UPDATE objekte SET problem=$problem WHERE id=$id", ("$problem", ""), ("$id", 0)); update.Prepare();
        using (var cmd = Befehl(c, "SELECT id,coalesce(aktuell,original),namensschluessel FROM objekte ORDER BY id"))
        using (var r = cmd.ExecuteReader()) while (r.Read())
        {
            token.ThrowIfCancellationRequested(); anzahl++;
            var q = XtfLieferungsXml.Beleg(XtfLieferungsXml.Element(r.GetString(1)));
            var problem = q.Kennung.Length > 0 && ids.GetValueOrDefault(q.Kennung) == "doppelte Kennung" ? "Originalkennung kommt doppelt vor; keine automatische Auswahl."
                : !r.IsDBNull(2) && doppelteNamen.Contains(r.GetString(2)) ? "Bezeichnung ist im gemeinsamen Namensraum nicht eindeutig."
                : XtfLieferungsNorm.Problem(q, id => ids.GetValueOrDefault(id));
            foreach (var (rolle, id) in q.Referenzen) if (XtfLieferungsNorm.ExterneOrganisation(rolle) && !ids.ContainsKey(id)) externe.Add(id);
            if (problem is not null)
            {
                fehler++; update.Parameters["$problem"].Value = problem; update.Parameters["$id"].Value = r.GetInt64(0); update.ExecuteNonQuery();
                details.AppendLine($"Zeile {r.GetInt64(0)}: {q.Klasse} «{q.Werte.GetValueOrDefault("Bezeichnung", q.Kennung)}» ({q.Kennung}): {problem}");
            }
            if (anzahl % 5000 == 0) fortschritt?.Report($"{anzahl:N0} Objekte geprüft; {fehler:N0} mit offenen Angaben …");
        }
        var bericht = $"{anzahl:N0} Objekte und Beziehungen geprüft.\n{fehler:N0} Objekte mit offenen Angaben.\n"
            + (fehler > 0 ? "Keine XTF erstellt. Je Objekt ist der erste festgestellte Fehler genannt; nach Korrekturen erneut prüfen.\n" : "Objekt-, Feld- und Beziehungsprüfung bestanden.\n")
            + $"{externe.Count:N0} externe Organisationskennungen fehlen als Stammdaten in dieser Datei. Sie müssen im Ziel vorhanden sein.\n"
            + "Diese Prüfung ersetzt nicht die vollständige INTERLIS-Modellprüfung und die GEONIS-Abnahme.\n\n" + details;
        return new(anzahl, fehler, externe.Count, bericht);
    }

    private static void Schreibe(SqliteConnection c, string datei, CancellationToken token)
    {
        using var stream = new FileStream(datei, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var w = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false });
        w.WriteStartDocument(); var transfer = XtfLieferungsXml.Element(Meta(c, "transfer")); Start(w, transfer);
        var header = XtfLieferungsXml.Element(Meta(c, "header")); header.SetAttributeValue("SENDER", "SewerStudio"); header.WriteTo(w);
        w.WriteStartElement("DATASECTION", XtfLieferungsXml.Ns.NamespaceName);
        var koerbe = new List<(long Id, XElement Huelle)>();
        using (var cmd = Befehl(c, "SELECT id,xml FROM koerbe ORDER BY id"))
        using (var r = cmd.ExecuteReader()) while (r.Read()) koerbe.Add((r.GetInt64(0), XtfLieferungsXml.Element(r.GetString(1))));
        foreach (var (id, huelle) in koerbe)
        {
            token.ThrowIfCancellationRequested(); Start(w, huelle);
            using var cmd = Befehl(c, "SELECT coalesce(aktuell,original) FROM objekte WHERE korb=$id ORDER BY id", ("$id", id));
            using var r = cmd.ExecuteReader();
            while (r.Read()) { token.ThrowIfCancellationRequested(); XtfLieferungsXml.FuerExport(XtfLieferungsXml.Element(r.GetString(0))).WriteTo(w); }
            w.WriteEndElement();
        }
        w.WriteEndElement(); w.WriteEndElement(); w.WriteEndDocument(); w.Flush(); stream.Flush(true);
    }
    private static void Start(XmlWriter writer, XElement e)
    {
        writer.WriteStartElement(e.Name.LocalName, e.Name.NamespaceName);
        foreach (var a in e.Attributes())
        {
            if (a.IsNamespaceDeclaration)
            {
                if (a.Name.LocalName == "xmlns") writer.WriteAttributeString("xmlns", a.Value);
                else writer.WriteAttributeString("xmlns", a.Name.LocalName, XNamespace.Xmlns.NamespaceName, a.Value);
            }
            else writer.WriteAttributeString(a.Name.LocalName, a.Name.NamespaceName, a.Value);
        }
    }
}
