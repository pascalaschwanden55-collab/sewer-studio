using AuswertungPro.Next.Application.Xtf.Lieferung;
using System.Xml.Linq;
using static AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung.XtfLieferungsDatenbank;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung;

public sealed class XtfLieferungsAblage : IXtfLieferungsAblage
{
    public XtfLieferungsInfo Importiere(string quelle, string arbeitsdatei, IProgress<string>? fortschritt = null, CancellationToken token = default)
    {
        XtfLieferungsImport.Importiere(quelle, arbeitsdatei, fortschritt, token); return Oeffne(arbeitsdatei);
    }
    public XtfLieferungsInfo Oeffne(string arbeitsdatei)
    {
        using var c = Verbinde(arbeitsdatei); var gruppen = new List<XtfLieferungsGruppe>();
        using (var cmd = Befehl(c, "SELECT klasse,COUNT(*) FROM objekte GROUP BY klasse ORDER BY klasse"))
        using (var r = cmd.ExecuteReader()) while (r.Read()) gruppen.Add(new(r.GetString(0), r.GetInt64(1)));
        return new(Path.GetFullPath(arbeitsdatei), Meta(c, "quelle"), Meta(c, "sha256"), gruppen.Sum(g => g.Anzahl),
            Convert.ToInt64(Skalar(c, "SELECT COUNT(*) FROM objekte WHERE aktuell IS NOT NULL")), gruppen);
    }
    public XtfLieferungsSeite Suche(string arbeitsdatei, string? klasse, string suche, int seite = 0, bool nurProbleme = false)
    {
        if (seite < 0 || seite > 20_000_000) throw new ArgumentOutOfRangeException(nameof(seite));
        using var c = Verbinde(arbeitsdatei);
        var filter = " WHERE ($klasse='' OR klasse=$klasse) AND ($suche='' OR instr(lower(name),lower($suche))>0 OR instr(lower(coalesce(tid,'')),lower($suche))>0) AND ($probleme=0 OR problem<>'')";
        var parameter = new (string, object?)[] { ("$klasse", klasse ?? ""), ("$suche", suche.Trim()), ("$probleme", nurProbleme ? 1 : 0) };
        var count = Convert.ToInt64(Skalar(c, "SELECT COUNT(*) FROM objekte" + filter, parameter));
        using var cmd = Befehl(c, "SELECT id,klasse,coalesce(tid,''),name,aktuell IS NOT NULL,problem FROM objekte" + filter + " ORDER BY id LIMIT 100 OFFSET $offset",
            parameter.Concat(new (string, object?)[] { ("$offset", (long)seite * 100) }).ToArray());
        using var r = cmd.ExecuteReader(); var zeilen = new List<XtfLieferungsZeile>();
        while (r.Read()) zeilen.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.GetString(5)));
        return new(count, zeilen);
    }
    public XtfLieferungsObjekt Lies(string arbeitsdatei, long id)
    {
        using var c = Verbinde(arbeitsdatei); using var cmd = Befehl(c, "SELECT version,coalesce(aktuell,original) FROM objekte WHERE id=$id", ("$id", id));
        using var r = cmd.ExecuteReader(); if (!r.Read()) throw new InvalidOperationException("Das Objekt ist nicht mehr vorhanden.");
        var e = XtfLieferungsXml.Element(r.GetString(1)); var q = XtfLieferungsXml.Beleg(e);
        return new(id, r.GetInt32(0), q.Klasse, q.Kennung, XtfLieferungsXml.Felder(e));
    }
    public void Speichere(string arbeitsdatei, long id, int version, IReadOnlyDictionary<string, string> aenderungen)
    {
        using var c = Verbinde(arbeitsdatei, schreiben: true); using var tx = c.BeginTransaction();
        XElement e; string original;
        using (var cmd = Befehl(c, "SELECT version,coalesce(aktuell,original),original FROM objekte WHERE id=$id", ("$id", id)))
        using (var r = cmd.ExecuteReader())
        {
            if (!r.Read() || r.GetInt32(0) != version) throw new InvalidOperationException("Das Objekt wurde inzwischen geändert. Bitte neu laden; nichts wurde überschrieben.");
            e = XtfLieferungsXml.Element(r.GetString(1)); original = r.GetString(2);
        }
        string? Zielklasse(string tid)
        {
            using var cmd = Befehl(c, "SELECT klasse FROM objekte WHERE tid=$tid LIMIT 2", ("$tid", tid)); using var r = cmd.ExecuteReader();
            if (!r.Read()) return null; var klasse = r.GetString(0); return r.Read() ? "mehrdeutige Kennung" : klasse;
        }
        var neu = XtfLieferungsXml.Aendere(e, aenderungen, Zielklasse);
        if (!XNode.DeepEquals(e, neu))
        {
            var q = XtfLieferungsXml.Beleg(neu); var xml = neu.ToString(SaveOptions.DisableFormatting);
            Ausfuehren(c, "UPDATE objekte SET aktuell=$xml,name=$name,namensschluessel=$nkey,version=version+1,problem='' WHERE id=$id",
                ("$xml", XNode.DeepEquals(neu, XtfLieferungsXml.Element(original)) ? null : xml),
                ("$name", q.Werte.GetValueOrDefault("Bezeichnung") ?? q.Werte.GetValueOrDefault("Textinhalt", "")),
                ("$nkey", XtfLieferungsNorm.Namensschluessel(q)), ("$id", id));
            SetzeMeta(c, "pruefbericht", "");
        }
        tx.Commit();
    }
    public XtfLieferungsPruefung Pruefe(string arbeitsdatei, IProgress<string>? fortschritt = null, CancellationToken token = default)
        => XtfLieferungsAusgabe.Ausfuehren(arbeitsdatei, null, fortschritt, token);
    public XtfLieferungsPruefung Exportiere(string arbeitsdatei, string ziel, IProgress<string>? fortschritt = null, CancellationToken token = default)
        => XtfLieferungsAusgabe.Ausfuehren(arbeitsdatei, ziel, fortschritt, token);
    public void SichereBericht(string arbeitsdatei, string ziel)
    {
        using var c = Verbinde(arbeitsdatei); var bericht = Meta(c, "pruefbericht");
        if (bericht.Length == 0) throw new InvalidOperationException("Bitte die Lieferung zuerst prüfen.");
        using var file = new FileStream(ziel, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(file, new System.Text.UTF8Encoding(false)); writer.Write(bericht);
    }
}
