using AuswertungPro.Next.Application.Xtf.Dss;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Schreibgeschützte Schachtanzeigen der Einbaumasken über die belegte Knotenkennung lesen.</summary>
internal static class ObjektaktenSchachtVererbung
{
    internal static bool Lies(ObjektaktenBearbeitung b, ObjektAkte akte, ObjektFeldDefinition feld, out string text)
    {
        text = "";
        if (feld.ErbtVon != "schacht" || !feld.NurLesen || akte.Art is not ("pumpe" or "ueberlauf" or "absperr_drossel")) return false;
        var key = feld.Id[(akte.Art.Length + 1)..] switch
        {
            "status" => "betriebsstatus", "tiefe_m" => "tiefe", "inspektionsintervall_jahr" => "inspektionsintervall",
            "spuelintervall_jahr" => "spuelintervall", "erstellt_am_utc" => "erstellt_am", "geaendert_am_utc" => "geaendert_am",
            var suffix => suffix
        };
        var schachtfeld = FieldCatalog.Objektfelder.Felder.SingleOrDefault(f => f.Id == "schacht." + key);
        if (schachtfeld is null) return true;
        var einbauIds = akte.Quellen.Where(q => DssEinbautenZuordnung.Art(q.Klasse) == akte.Art).Select(q => q.Kennung).Distinct().ToArray();
        var original = einbauIds.Length == 1 ? Quelle(akte.Quellen, einbauIds[0]) : null;
        var knotenfeld = akte.Art + (akte.Art == "ueberlauf" ? ".knoten_von" : ".knoten");
        var knotenId = akte.Werte.TryGetValue(knotenfeld, out var eingabe) ? eingabe.Text : original?.Referenzen.GetValueOrDefault("AbwasserknotenRef");
        if (string.IsNullOrEmpty(knotenId)) return true;
        var schaechte = b.Projekt.SchaechteData.Where(s => s.Geonis?.Knoten == knotenId).ToArray();
        if (schaechte.Length == 1)
        {
            var parent = new ObjektaktenBearbeitung(b.Projekt, schaechte[0].Id, "schacht");
            text = parent.Lies(parent.Wurzel, schachtfeld);
            // Auch das bewusste Leeren des Projektfeldes ist ein aktueller Wert.
            if (parent.Wurzel.Werte.ContainsKey(schachtfeld.Id)
                || schachtfeld.Speicherfeld is { } speicher && (text.Length > 0
                    || schaechte[0].FieldMeta.GetValueOrDefault(SchachtFeldnamen.Feld(schaechte[0], speicher))?.UserEdited == true)) return true;
        }
        else if (schaechte.Length > 1) return true;
        var quellen = b.Projekt.Objektakten.SelectMany(a => a.Quellen).ToArray();
        var knoten = Quelle(quellen, knotenId);
        if (knoten?.Klasse != "Abwasserknoten" || DssFeldZuordnung.Ziel(schachtfeld) is not { } ziel) return true;
        var quelle = ziel.Klasse == "Abwasserknoten" ? knoten
            : ziel.Klasse == "Normschacht" ? Quelle(quellen, knoten.Referenzen.GetValueOrDefault("AbwasserbauwerkRef")) : null;
        if (quelle is null) return true;
        var wert = quelle.Werte.GetValueOrDefault(ziel.Attribut) ?? quelle.Referenzen.GetValueOrDefault(ziel.Attribut);
        if (ziel.Attribut is "EigentuemerRef" or "BetreiberRef")
            text = Quelle(quellen, wert)?.Werte.GetValueOrDefault("Bezeichnung") ?? wert ?? "";
        else text = DssFeldZuordnung.KatalogAnzeige(schachtfeld, wert) ?? "";
        return true;
    }

    private static ObjektQuellbeleg? Quelle(IEnumerable<ObjektQuellbeleg> quellen, string? id)
    {
        if (id is null) return null;
        var treffer = quellen.Where(q => q.Kennung == id).OrderByDescending(q => q.ImportiertUtc).ToArray();
        if (treffer.Length == 0) return null;
        var neu = treffer[0];
        return treffer.Any(q => q.ImportiertUtc == neu.ImportiertUtc && !GeoShopObjektaktenImport.Gleich(neu, q)) ? null : neu;
    }
}
