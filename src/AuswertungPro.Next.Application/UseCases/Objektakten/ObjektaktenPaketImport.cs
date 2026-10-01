using System.Text.Json;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Ergaenzt nur Fehlendes bei exakt derselben Projekt-/Objektidentitaet.</summary>
public static class ObjektaktenPaketImport
{
    public static string Pruefe(Project projekt, ObjektaktenPaket paket)
    {
        ObjektaktenStruktur.Pruefe(projekt);
        if (paket.Format != "SewerStudio.Objektakten" || paket.Version != 1 || paket.Akten is null
            || paket.Haltungsfelder is null || paket.Schachtfelder is null || paket.Haltungsmetadaten is null || paket.Schachtmetadaten is null)
            throw new InvalidOperationException("Unbekanntes oder beschädigtes Zusatzformat.");
        if (paket.Akten.Any(a => a is null || a.Id == Guid.Empty || a.Art is not ("haltung" or "schacht" or "deckel" or "sanierung")
            || a.Werte is null || a.Quellen is null || a.Bezuege is null || a.Unterlisten is null
            || a.Werte.Any(w => w.Value is null || w.Value.Text is null)
            || a.Quellen.Any(q => q is null || q.Werte is null || q.Referenzen is null || q.Strukturen is null)
            || a.Unterlisten.Any(l => l.Value is null || l.Value.Any(z => z is null)))
            || paket.Akten.Select(a => a.Id).Distinct().Count() != paket.Akten.Count)
            throw new InvalidOperationException("Beschädigte oder doppelte Objektakte.");
        foreach (var felder in paket.Haltungsfelder.Values.Concat(paket.Schachtfelder.Values))
            if (felder is null || felder.Any(f => string.IsNullOrWhiteSpace(f.Key) || f.Value is null))
                throw new InvalidOperationException("Beschädigte Bestandsfelder.");
        foreach (var meta in paket.Haltungsmetadaten.Values.Concat(paket.Schachtmetadaten.Values))
            if (meta is null || meta.Any(f => f.Value is null)) throw new InvalidOperationException("Beschädigte Feldherkunft.");
        if (paket.ProjektId != projekt.Id) throw new InvalidOperationException("Die Zusatzdatei gehört zu einem anderen Projekt. Keine Zuordnung nur nach Namen.");
        var haltungen = projekt.Data.Select(r => r.Id).ToHashSet();
        var schaechte = projekt.SchaechteData.Select(r => r.Id).ToHashSet();
        if (paket.Haltungsfelder.Keys.Any(id => !haltungen.Contains(id)) || paket.Schachtfelder.Keys.Any(id => !schaechte.Contains(id)))
            throw new InvalidOperationException("Die Zusatzdatei enthält unbekannte Haltungen oder Schächte.");
        foreach (var a in paket.Akten)
        {
            if (a.Art == "haltung" && !haltungen.Contains(a.Id) || a.Art == "schacht" && !schaechte.Contains(a.Id)
                || a.Art is "deckel" or "sanierung" && (haltungen.Contains(a.Id) || schaechte.Contains(a.Id)))
                throw new InvalidOperationException("Objektart und Kennung passen nicht zusammen.");
            if (a.Art is "deckel" or "sanierung" && (a.Bezuege.Count == 0 || a.Bezuege.Any(id =>
                !schaechte.Contains(id) && (a.Art == "deckel" || !haltungen.Contains(id)))))
                throw new InvalidOperationException("Unterobjekt ohne gültigen Bauwerksbezug.");
            if (a.HauptdeckelId is { } deckel && (a.Art != "schacht" || !paket.Akten.Concat(projekt.Objektakten)
                .Any(d => d.Id == deckel && d.Art == "deckel" && d.Bezuege.Contains(a.Id))))
                throw new InvalidOperationException("Der Hauptdeckel gehört nicht zum Schacht.");
        }
        var bekannte = projekt.Data.Select(r => r.Id).Concat(projekt.SchaechteData.Select(r => r.Id))
            .Concat(paket.Akten.Select(a => a.Id)).ToHashSet();
        if (paket.Akten.Any(a => a.Bezuege.Any(id => !bekannte.Contains(id))))
            throw new InvalidOperationException("Die Zusatzdatei enthält nicht auflösbare lokale Objektbeziehungen.");
        foreach (var a in paket.Akten)
        {
            var alt = projekt.Objektakten.SingleOrDefault(x => x.Id == a.Id);
            if (alt is not null && alt.Art != a.Art) throw new InvalidOperationException("Widersprüchliche Objektart bei gleicher Kennung.");
        }
        return $"Zusatzdatei: {paket.Akten.Count} Objektakten. Fehlende Akten und Felder ergänzen. " +
            "Gefüllte und von Hand geleerte Werte bleiben bestehen. Abweichende vorhandene Werte werden nicht übernommen. " +
            "Es werden keine Haltungen oder Schächte neu angelegt.";
    }

    private static bool DarfErgaenzen(string art, string key) => !FieldCatalog.Objektfelder.Felder
        .Any(f => f.Art == art && f.NurLesen && f.Speicherfeld == key)
        && key is not ("Schachtnummer" or "Haltungsname" or "Schacht_oben" or "Schacht_unten")
        && !key.StartsWith("__", StringComparison.Ordinal) && !VirtuelleSpalte.IstVirtuell(key);

    public static void Uebernehme(Project projekt, ObjektaktenPaket paket)
    {
        Pruefe(projekt, paket);
        // Alle fremden Daten vor dem ersten Schreibzugriff prüfen und vom Aufrufer entkoppeln.
        paket = JsonSerializer.Deserialize<ObjektaktenPaket>(JsonSerializer.Serialize(paket))!;
        foreach (var r in projekt.Data)
            if (paket.Haltungsfelder.TryGetValue(r.Id, out var felder))
                foreach (var f in felder.Where(f => DarfErgaenzen("haltung", f.Key) && string.IsNullOrEmpty(r.GetFieldValue(f.Key)) && !r.FieldMeta.GetValueOrDefault(f.Key, new()).UserEdited))
                {
                    var meta = paket.Haltungsmetadaten.GetValueOrDefault(r.Id)?.GetValueOrDefault(f.Key);
                    r.SetFieldValue(f.Key, f.Value, meta?.Source ?? FieldSource.Kataster, meta?.UserEdited ?? false);
                    if (meta is not null) r.FieldMeta[f.Key] = meta;
                }
        foreach (var r in projekt.SchaechteData)
            if (paket.Schachtfelder.TryGetValue(r.Id, out var felder))
                foreach (var f in felder)
                {
                    var key = SchachtFeldnamen.Feld(r, f.Key);
                    if (DarfErgaenzen("schacht", key) && string.IsNullOrEmpty(r.GetFieldValue(key)) && !r.IsUserEdited(key))
                    {
                        var meta = paket.Schachtmetadaten.GetValueOrDefault(r.Id)?.GetValueOrDefault(f.Key);
                        r.SetFieldValue(key, f.Value, meta?.Source ?? FieldSource.Kataster, meta?.UserEdited ?? false);
                        if (meta is not null) r.FieldMeta[key] = meta;
                    }
                }
        foreach (var quelle in paket.Akten)
        {
            var kopie = quelle;
            var alt = projekt.Objektakten.SingleOrDefault(a => a.Id == quelle.Id);
            if (alt is null) { projekt.Objektakten.Add(kopie); continue; }
            foreach (var f in kopie.Werte)
                if (!alt.Werte.TryGetValue(f.Key, out var w) || !w.VonHand && w.Text.Length == 0) alt.Werte[f.Key] = f.Value;
            foreach (var q in kopie.Quellen)
                if (!alt.Quellen.Any(a => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(q))) alt.Quellen.Add(q);
            foreach (var b in kopie.Bezuege) if (!alt.Bezuege.Contains(b)) alt.Bezuege.Add(b);
            foreach (var t in kopie.Unterlisten) alt.Unterlisten.TryAdd(t.Key, t.Value);
            if (kopie.Zusatzdaten is not null)
            {
                alt.Zusatzdaten ??= new();
                foreach (var f in kopie.Zusatzdaten) alt.Zusatzdaten.TryAdd(f.Key, f.Value);
            }
            alt.HauptdeckelId ??= kopie.HauptdeckelId;
        }
        projekt.Version = Math.Max(3, projekt.Version); projekt.Dirty = true;
    }
}
