using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Xtf.Dss;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Nur belegte Attribute fuellen Zusatzfelder. Alle gelieferten Rohwerte bleiben daneben erhalten.</summary>
public static class GeoShopObjektaktenImport
{
    public static bool HatNeueQuellen(Project projekt, Guid id, GeoShopBauteil quelle)
    {
        var akte = projekt.Objektakten.SingleOrDefault(a => a.Id == id);
        return quelle.Quellen?.Any(q => akte is null || !akte.Quellen.Any(a => Gleich(a, q))) == true;
    }

    public static string Stand(Project projekt, Guid id) => JsonSerializer.Serialize(projekt.Objektakten
        .Where(a => a.Id == id || a.Bezuege.Contains(id)));

    public static void Uebernehme(Project projekt, Guid id, string art, GeoShopBauteil quelle, bool gedreht)
    {
        if (quelle.Quellen is not { Count: > 0 } quellen) return;
        var root = projekt.Objektakten.SingleOrDefault(a => a.Id == id);
        if (root is null) { root = new ObjektAkte { Id = id, Art = art }; projekt.Objektakten.Add(root); }
        foreach (var q in quellen)
        {
            if (!root.Quellen.Any(a => Gleich(a, q))) root.Quellen.Add(Kopie(q));
        }
        var k = quelle.Kennungen;
        var primaer = quellen.SingleOrDefault(q => q.Kennung == (art == "haltung" ? k.Haltung : k.Knoten));
        var bauwerk = quellen.SingleOrDefault(q => q.Kennung == (art == "haltung" ? k.Kanal : k.Bauwerk));
        var von = quellen.SingleOrDefault(q => q.Kennung == (gedreht ? k.NachPunkt : k.VonPunkt));
        var nach = quellen.SingleOrDefault(q => q.Kennung == (gedreht ? k.VonPunkt : k.NachPunkt));
        foreach (var feld in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == art && f.Speicherfeld is null))
        {
            string? wert = feld.Id switch
            {
                "haltung.fromlevel" => Wert(von, "Kote"),
                "haltung.tolevel" => Wert(nach, "Kote"),
                "haltung.fromheightaccuracy" => Wert(von, "Hoehengenauigkeit"),
                "haltung.toheightaccuracy" => Wert(nach, "Hoehengenauigkeit"),
                "haltung.frompoint" => von?.Kennung,
                "haltung.topoint" => nach?.Kennung,
                "haltung.profileref" => k.Rohrprofil,
                _ => AusAttribut(feld.Exportziel, [primaer, bauwerk]) ?? DssWert(feld, primaer, bauwerk, von, nach)
            };
            Fuellen(root, feld.Id, wert);
        }
        // Die Hauptdeckelwahl wird nicht aus einer blossen Einzelmenge erfunden.
        foreach (var q in quellen.Where(q => q.Klasse is "Deckel" or "Unterhalt"))
        {
            if (q.Klasse == "Unterhalt" && !q.Werte.GetValueOrDefault("Art", "").StartsWith("Sanierung_", StringComparison.Ordinal)) continue;
            if (q.Klasse == "Deckel" && (art != "schacht" || q.Referenzen.GetValueOrDefault("AbwasserbauwerkRef") != k.Bauwerk)) continue;
            var subart = q.Klasse == "Deckel" ? "deckel" : "sanierung";
            var sub = projekt.Objektakten.FirstOrDefault(a => a.Art == subart && a.Quellen.Any(s => s.Modell == q.Modell && s.Kennung == q.Kennung));
            if (sub is null)
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(q.Modell + ":" + q.Klasse + ":" + q.Kennung));
                sub = new ObjektAkte { Id = new Guid(hash.AsSpan(0, 16)), Art = subart };
                projekt.Objektakten.Add(sub);
            }
            if (!sub.Bezuege.Contains(id)) sub.Bezuege.Add(id);
            if (!sub.Quellen.Any(a => Gleich(a, q))) sub.Quellen.Add(Kopie(q));
            foreach (var feld in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == subart))
                Fuellen(sub, feld.Id, AusAttribut(feld.Exportziel, [q]) ?? DssWert(feld, q, null, null, null));
            if (subart == "sanierung")
            {
                var firmenbelege = quellen.Where(s => s.Klasse == "Erhaltungsereignis_Ausfuehrende_FirmaAssoc"
                    && s.Referenzen.GetValueOrDefault("Erhaltungsereignis_Ausfuehrende_FirmaAssocRef") == q.Kennung).ToArray();
                var firmenIds = firmenbelege.Select(s => s.Referenzen.GetValueOrDefault("Ausfuehrende_FirmaRef"))
                    .Append(q.Referenzen.GetValueOrDefault("Ausfuehrende_FirmaRef"))
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
                var firmen = quellen.Where(s => s.Klasse == "Organisation" && firmenIds.Contains(s.Kennung)
                    && s.System == q.System).ToArray();
                foreach (var beleg in firmenbelege.Concat(firmen))
                    if (!sub.Quellen.Any(s => Gleich(s, beleg))) sub.Quellen.Add(Kopie(beleg));
                if (firmenIds.Length == 1 && firmen.Length == 1)
                    Fuellen(sub, "sanierung.firma", Wert(firmen[0], "Bezeichnung"));
                foreach (var (feld, attribut) in new[] { ("bemerkung", "Bemerkung"), ("ausfuehrender", "Ausfuehrender"),
                    ("datengrundlage", "Datengrundlage"), ("dauer", "Dauer"), ("detaildaten", "Detaildaten"),
                    ("ergebnis", "Ergebnis"), ("grund", "Grund"), ("kosten", "Kosten") })
                    Fuellen(sub, "sanierung." + feld, Wert(q, attribut));
            }
        }
        projekt.Version = Math.Max(3, projekt.Version); projekt.Dirty = true; projekt.ModifiedAtUtc = DateTime.UtcNow;
    }

    private static void Fuellen(ObjektAkte akte, string feld, string? wert)
    {
        if (wert is null) return;
        if (akte.Werte.TryGetValue(feld, out var alt) && (alt.VonHand || alt.Text.Length > 0)) return;
        akte.Werte[feld] = new ObjektFeldWert { Text = wert, GeaendertUtc = DateTime.UtcNow };
    }
    private static string? Wert(ObjektQuellbeleg? q, string feld) => q?.Werte.GetValueOrDefault(feld) ?? q?.Referenzen.GetValueOrDefault(feld);
    private static string? DssWert(ObjektFeldDefinition feld, ObjektQuellbeleg? primaer, ObjektQuellbeleg? bauwerk, ObjektQuellbeleg? von, ObjektQuellbeleg? nach)
    {
        if (DssFeldZuordnung.Ziel(feld) is not { } ziel) return null;
        var q = ziel.Klasse switch
        {
            "von" => von, "nach" => nach,
            _ when primaer?.Klasse == ziel.Klasse => primaer,
            _ when bauwerk?.Klasse == ziel.Klasse || ziel.Klasse == "Normschacht" => bauwerk,
            _ => null
        };
        return Wert(q, ziel.Attribut);
    }
    private static string? AusAttribut(string? ziel, IEnumerable<ObjektQuellbeleg?> quellen)
    {
        if (ziel is null) return null;
        var m = System.Text.RegularExpressions.Regex.Match(ziel, @"^([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)(?: \(geerbt\))?$");
        if (!m.Success) return null;
        var q = quellen.FirstOrDefault(q => q?.Klasse == m.Groups[1].Value);
        return Wert(q, m.Groups[2].Value);
    }
    private static bool Gleich(ObjektQuellbeleg a, ObjektQuellbeleg b) => a.Modell == b.Modell && a.Klasse == b.Klasse && a.Kennung == b.Kennung
        && a.Werte.Count == b.Werte.Count && a.Werte.All(p => b.Werte.TryGetValue(p.Key, out var v) && v == p.Value)
        && a.Referenzen.Count == b.Referenzen.Count && a.Referenzen.All(p => b.Referenzen.TryGetValue(p.Key, out var v) && v == p.Value)
        && a.Strukturen.Count == b.Strukturen.Count && a.Strukturen.All(p => b.Strukturen.TryGetValue(p.Key, out var v) && v == p.Value);
    private static ObjektQuellbeleg Kopie(ObjektQuellbeleg q) => new()
    {
        System = q.System, Datei = q.Datei, Modell = q.Modell, Klasse = q.Klasse, Kennung = q.Kennung,
        IstLokaleKennung = q.IstLokaleKennung, ImportiertUtc = DateTime.UtcNow, Werte = new(q.Werte), Referenzen = new(q.Referenzen), Strukturen = new(q.Strukturen)
    };
}
