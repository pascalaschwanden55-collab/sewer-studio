using System.Text.Json;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>Ein gemeinsamer Vergleich fuer Bestandsfelder und alle angebundenen Objektakten.</summary>
public sealed class GeoShopImportVergleich
{
    public IReadOnlyList<GeoShopFeldWahl> Felder { get; }
    public IReadOnlyList<string> Hinweise { get; }
    private readonly IReadOnlyList<ObjektAkte> _kandidaten;
    private readonly string _merkfeld;
    private readonly Dictionary<string, string[]> _entscheidungen;
    internal bool HatNeueAkten { get; }

    private GeoShopImportVergleich(List<GeoShopFeldWahl> felder, List<string> hinweise, List<ObjektAkte> kandidaten,
        string merkfeld, Dictionary<string, string[]> entscheidungen, bool hatNeueAkten)
    { Felder = felder; Hinweise = hinweise; _kandidaten = kandidaten; _merkfeld = merkfeld; _entscheidungen = entscheidungen; HatNeueAkten = hatNeueAkten; }

    public static GeoShopImportVergleich Baue(GeoShopZiel ziel, GeoShopBauteil quelle,
        IReadOnlyDictionary<string, string> werte, bool gedreht)
    {
        var projekt = ziel.Projekt ?? throw new InvalidOperationException("Der Vergleich benötigt ein Projekt.");
        var art = ziel.Art == BauteilArt.Schacht ? "schacht" : "haltung";
        var leer = new Project { Id = projekt.Id };
        if (ziel.Art == BauteilArt.Schacht) leer.SchaechteData.Add(new SchachtRecord { Id = ziel.Id });
        else leer.Data.Add(new HaltungRecord { Id = ziel.Id });
        // Der bestehende Import ist die einzige Zuordnung. Er arbeitet hier nur auf einem leeren Entwurf.
        GeoShopObjektaktenImport.Uebernehme(leer, ziel.Id, art, quelle, gedreht);
        var merkfeld = $"GeoShop.Vergleich.{ziel.Id:N}";
        var entscheidungen = LadeEntscheidungen(projekt, merkfeld);
        var felder = new List<GeoShopFeldWahl>();
        bool BrauchtVergleich(GeoShopFeldWahl w)
        {
            if (GeoShopFeldWahl.Gleich(w.Vorher, w.Nachher)) return false;
            return !entscheidungen.TryGetValue(w.Schluessel, out var alt)
                || !GeoShopFeldWahl.Gleich(alt[0], w.Nachher) || !GeoShopFeldWahl.Gleich(alt[1], w.Vorher);
        }
        foreach (var (feld, wert) in werte)
        {
            if (ziel.Art == BauteilArt.Haltung && GeoShopAbgleichPlanBuilder.ImmerAusXtf.Contains(feld)) continue;
            var definition = FieldCatalog.Objektfelder.Felder.FirstOrDefault(f => f.Art == art && f.Speicherfeld == feld);
            felder.Add(new(ziel.Id, feld, true, ziel.Name, definition?.Label ?? FieldCatalog.Get(feld).Label,
                ziel.Wert(feld), wert, ziel.Herkunft(feld), ziel.Handgesetzt(feld)));
        }
        foreach (var akte in leer.Objektakten)
        {
            if (projekt.Objektakten.Any(a => a.Id == akte.Id && a.Art != akte.Art))
                throw new InvalidOperationException("Eine Objektkennung gehört inzwischen zu einer anderen Objektart. Bitte die Zuordnung prüfen.");
            var treffer = projekt.Objektakten.Where(a => (a.Art == akte.Art
                || a.Art is "sanierung" or "unterhalt" && akte.Art is "sanierung" or "unterhalt") && (a.Id == akte.Id || akte.Id != ziel.Id
                && a.Quellen.Any(q => akte.Quellen.Any(n => Identisch(q, n))))).ToArray();
            if (treffer.Length > 1) throw new InvalidOperationException("Eine Quellkennung gehört zu mehreren Objektakten.");
            var alt = treffer.SingleOrDefault();
            if (alt is not null && alt.Art != akte.Art)
                throw new InvalidOperationException("Die Art eines verknüpften Ereignisses weicht ab. Bitte die Objektakte prüfen.");
            if (alt is not null) akte.Id = alt.Id;
            foreach (var (feld, wert) in akte.Werte)
            {
                var vorher = alt?.Werte.GetValueOrDefault(feld);
                var f = FieldCatalog.Objektfelder.Felder.FirstOrDefault(f => f.Id == feld);
                felder.Add(new(akte.Id, feld, false, akte.Id == ziel.Id ? ziel.Name : $"{ziel.Name} · {akte}",
                    f?.Label ?? feld, vorher?.Text ?? "", wert.Text,
                    vorher?.VonHand == true ? "Handeingabe" : vorher is null ? "Leer" : "Bisherige Objektakte", vorher?.VonHand == true));
            }
        }
        var hinweise = new List<string>();
        foreach (var f in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == art))
        {
            if (f.Id is "schacht.deckelhoehe" or "schacht.tiefe") continue;
            if (f.Id == "schacht.materialgruppe")
            { hinweise.Add($"{ziel.Name}: Eine eindeutige Materialgruppe wird aus dem gewählten Material angezeigt; ein genaueres Materialdetail wird nicht erfunden."); continue; }
            if (f.Speicherfeld is { } key ? werte.ContainsKey(key) : leer.Objektakten.Any(a => a.Id == ziel.Id && a.Werte.ContainsKey(f.Id))) continue;
            if (f.Id == "schacht.objectid")
            { hinweise.Add($"{ziel.Name}: Keine eigene OBJECTID geliefert. Ohne vorhandene OBJECTID zeigt das Feld die Schachtbezeichnung; die XTF-TIDs bleiben eigenständig."); continue; }
            hinweise.Add($"{ziel.Name}: {f.Label} — {(f.Belegstatus is "offen" or "zusatz" ? "keine belegte XTF-Zuordnung" : "nicht geliefert oder nicht eindeutig zuordenbar")}; vorhandene Angaben bleiben erhalten.");
        }
        foreach (var gruppe in felder.GroupBy(w => w.ObjektId))
            foreach (var (a, b) in new[] { ("schacht.rechtswert", "schacht.hochwert"), ("deckel.rechtswert", "deckel.hochwert"),
                ("haltungspunkt.rechtswert", "haltungspunkt.hochwert"),
                (FieldKeys.ShaftDimension1Mm, FieldKeys.ShaftDimension2Mm) })
            {
                var links = gruppe.SingleOrDefault(w => w.Feld == a); var rechts = gruppe.SingleOrDefault(w => w.Feld == b);
                if (links is null || rechts is null || GeoShopFeldWahl.Gleich(links.Vorher, links.Nachher)
                    || GeoShopFeldWahl.Gleich(rechts.Vorher, rechts.Nachher)) continue;
                var beide = links.Uebernehmen && rechts.Uebernehmen;
                links.Partner = rechts; rechts.Partner = links; links.Uebernehmen = beide;
            }
        // Ein früher abgelehnter Bestandteil muss wieder sichtbar werden, wenn sich sein Partner ändert.
        felder.RemoveAll(w => !BrauchtVergleich(w) && (w.Partner is null || !BrauchtVergleich(w.Partner)));
        var neueAkten = leer.Objektakten.Any(n => !projekt.Objektakten.Any(a => a.Id == n.Id)
            || n.Quellen.Any(q => !projekt.Objektakten.Single(a => a.Id == n.Id).Quellen.Any(s => GeoShopObjektaktenImport.Gleich(s, q)))
            || n.Bezuege.Any(b => !projekt.Objektakten.Single(a => a.Id == n.Id).Bezuege.Contains(b)));
        return new(felder, hinweise, leer.Objektakten, merkfeld, entscheidungen, neueAkten);
    }

    internal void Uebernehme(GeoShopZiel ziel)
    {
        var projekt = ziel.Projekt!;
        var entscheidungen = new Dictionary<string, string[]>(_entscheidungen);
        foreach (var kandidat in _kandidaten)
        {
            var akte = projekt.Objektakten.SingleOrDefault(a => a.Id == kandidat.Id);
            if (akte is null) { akte = new ObjektAkte { Id = kandidat.Id, Art = kandidat.Art }; projekt.Objektakten.Add(akte); }
            foreach (var id in kandidat.Bezuege) if (!akte.Bezuege.Contains(id)) akte.Bezuege.Add(id);
            foreach (var q in kandidat.Quellen)
            {
                // Aktueller Originalstand statt widerspruechlicher Versionen derselben TID im Export.
                if (akte.Quellen.Any(s => GeoShopObjektaktenImport.Gleich(s, q))) continue;
                foreach (var traeger in projekt.Objektakten.Where(a => a == akte || a.Quellen.Any(s => Identisch(s, q))))
                {
                    traeger.Quellen.RemoveAll(s => Identisch(s, q));
                    traeger.Quellen.Add(GeoShopObjektaktenImport.Kopie(q));
                }
            }
        }
        foreach (var w in Felder)
        {
            if (w.Uebernehmen)
            {
                if (w.Bestandsfeld) ziel.SchreibeVergleich(w.Feld, w.Nachher);
                else projekt.Objektakten.Single(a => a.Id == w.ObjektId).Werte[w.Feld] = new()
                { Text = w.Nachher, GeaendertUtc = DateTime.UtcNow };
            }
            else if (!w.Bestandsfeld)
                projekt.Objektakten.Single(a => a.Id == w.ObjektId).Werte.TryAdd(w.Feld,
                    new() { Text = w.Vorher, GeaendertUtc = DateTime.UtcNow });
            entscheidungen[w.Schluessel] = [w.Nachher, w.Uebernehmen ? w.Nachher : w.Vorher];
        }
        projekt.Metadata[_merkfeld] = JsonSerializer.Serialize(entscheidungen);
        projekt.Version = Math.Max(projekt.Version, 3);
        projekt.Dirty = true; projekt.ModifiedAtUtc = DateTime.UtcNow;
    }

    private static bool Identisch(ObjektQuellbeleg a, ObjektQuellbeleg b)
        => a.System == b.System && a.Modell == b.Modell && a.Klasse == b.Klasse && a.Kennung == b.Kennung;

    /// <summary>Der Normexport darf einen bewusst behaltenen Altwert nicht aus dem neuen Rohbeleg ersetzen.</summary>
    internal static bool BehaeltBestandswert(Project projekt, Guid id, string feld, string wert)
        => BehaeltWert(projekt, id, $"{id:N}|feld|{feld}", wert);

    internal static bool BehaeltAktenwert(Project projekt, ObjektAkte akte, string feld, string wert)
        => akte.Bezuege.Append(akte.Id).Any(id => BehaeltWert(projekt, id, $"{akte.Id:N}|akte|{feld}", wert));

    private static bool BehaeltWert(Project projekt, Guid id, string schluessel, string wert)
    {
        if (!projekt.Metadata.ContainsKey($"GeoShop.Vergleich.{id:N}")) return false;
        var stand = LadeEntscheidungen(projekt, $"GeoShop.Vergleich.{id:N}");
        return stand.TryGetValue(schluessel, out var w)
            && GeoShopFeldWahl.Gleich(w[1], wert) && !GeoShopFeldWahl.Gleich(w[0], wert);
    }

    private static Dictionary<string, string[]> LadeEntscheidungen(Project projekt, string key)
    {
        if (!projekt.Metadata.TryGetValue(key, out var json)) return new();
        var stand = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
        if (stand is null || stand.Values.Any(w => w is null || w.Length != 2 || w.Any(s => s is null)))
            throw new InvalidOperationException("Der gespeicherte GeoShop-Vergleich ist unlesbar.");
        return stand;
    }
}
