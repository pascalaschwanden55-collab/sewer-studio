using System.Text.Json;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Neu-Lieferung aus gespeicherten GeoShop-Belegen und aktuellen Eingaben. Kein Lesen der Kundendatei.</summary>
public static class DssExportPlanBuilder
{
    public static bool Benoetigt(Project p)
    {
        ObjektaktenStruktur.Pruefe(p);
        return p.Objektakten.Any(a => a.Quellen.Any(q => q.Modell == DssExportSchema.Modell)
            || a.Art is not ("haltung" or "schacht") || a.Werte.Any(v => v.Value.Text.Length > 0 || v.Value.VonHand));
    }

    public static XtfNeuPlan Build(Project projekt, IReadOnlyDictionary<string, XtfNeuGeometrie>? geometrien = null,
        bool mitZusatzangaben = false)
    {
        ObjektaktenStruktur.Pruefe(projekt);
        DssObjektaktenAbdeckung.Pruefe(projekt);
        var hinweise = new List<string> { "DSS_2020_1_LV95: belegte GeoShop-Felder, Deckel und Ereignisse als Normobjekte. Aktuelle Eingaben haben Vorrang." };
        var quellen = Quellen(projekt);
        var objekte = new Dictionary<string, DssExportObjekt>(StringComparer.Ordinal);
        var roots = new Dictionary<Guid, DssExportObjekt>();
        var importierteH = new HashSet<Guid>(); var importierteS = new HashSet<Guid>();
        foreach (var h in projekt.Data)
            if (Hole(h.Geonis?.Haltung, "Haltung") is { } o)
            {
                PruefeBauwerkskennung(h.Geonis?.Kanal, o);
                roots[h.Id] = o; importierteH.Add(h.Id);
                if (h.Geonis?.RichtungGedreht == true)
                {
                    (o.Refs["vonHaltungspunktRef"], o.Refs["nachHaltungspunktRef"]) = (o.Refs["nachHaltungspunktRef"], o.Refs["vonHaltungspunktRef"]);
                    if (o.Strukturen.TryGetValue("Verlauf", out var verlauf)) o.Strukturen["Verlauf"] = DssVerlaufRichtung.Drehe(verlauf);
                    o.Setze("Bezeichnung", h.GetFieldValue(FieldKeys.HoldingName));
                    hinweise.Add($"{o.Tid}: Haltungspunkte und Verlauf in Projektrichtung gedreht; Original-TIDs bleiben erhalten.");
                }
            }
        foreach (var s in projekt.SchaechteData)
            if (Hole(s.Geonis?.Knoten, "Abwasserknoten") is { } o)
            { PruefeBauwerkskennung(s.Geonis?.Bauwerk, o); roots[s.Id] = o; importierteS.Add(s.Id); }
        if (roots.Values.Select(o => o.Tid).Distinct(StringComparer.Ordinal).Count() != roots.Count)
            throw new InvalidOperationException("DSS: Mehrere Projektzeilen beanspruchen dieselbe Originalkennung. Bitte die Zuordnung zuerst bereinigen.");

        var neu = XtfNeuPlanBuilder.Build(projekt.Data.Where(h => !importierteH.Contains(h.Id)).ToArray(),
            projekt.SchaechteData.Where(s => !importierteS.Contains(s.Id)).ToArray(), projekt.Id.ToString("N"), geometrien);
        hinweise.AddRange(neu.Hinweise);
        foreach (var o in neu.Objekte) objekte.TryAdd(o.Tid, DssExportObjekt.Aus(o));
        foreach (var h in projekt.Data.Where(h => !roots.ContainsKey(h.Id)))
            if (objekte.Values.SingleOrDefault(o => o.Klasse == "Haltung" && o.Werte.GetValueOrDefault("Bezeichnung") == h.GetFieldValue(FieldKeys.HoldingName)) is { } o) roots[h.Id] = o;
        foreach (var s in projekt.SchaechteData.Where(s => !roots.ContainsKey(s.Id)))
            if (objekte.Values.SingleOrDefault(o => o.Klasse == "Abwasserknoten" && o.Werte.GetValueOrDefault("Bezeichnung") == XtfSchachtPlanBuilder.Wert(s, "Schachtnummer")) is { } o) roots[s.Id] = o;
        if (roots.Count != projekt.Data.Count + projekt.SchaechteData.Count)
            throw new InvalidOperationException("DSS: Nicht alle Projektobjekte erfüllen die Pflichtangaben. Keine unvollständige Lieferung erstellt. " + string.Join(" ", neu.Hinweise));

        // Eine eigene verknüpfte Akte kann ausserhalb der vorwärts erreichbaren
        // Hauptkette liegen (z.B. weiterer Zulaufpunkt am Schacht). Ihr Originalobjekt
        // mitnehmen, bevor die aktuellen Angaben darauf angewendet werden.
        foreach (var akte in projekt.Objektakten.Where(a => !roots.ContainsKey(a.Id)))
            foreach (var q in akte.Quellen.Where(q => !q.IstLokaleKennung
                && (q.Klasse == DssObjektarten.Klasse(akte.Art) || DssEinbautenZuordnung.Art(q.Klasse) == akte.Art)))
                Hole(q.Kennung);
        var bauwerke = roots.Values.Select(o => o.Refs.GetValueOrDefault("AbwasserbauwerkRef", "")).ToHashSet(StringComparer.Ordinal);
        var knoten = roots.Values.Where(o => o.Klasse == "Abwasserknoten").Select(o => o.Tid).ToHashSet(StringComparer.Ordinal);
        foreach (var q in quellen.Values.Where(q => q.Klasse == "Haltungspunkt" && knoten.Contains(q.Referenzen.GetValueOrDefault("AbwassernetzelementRef", "")))) Hole(q.Kennung);
        foreach (var q in quellen.Values.Where(q => q.Klasse is "Deckel" or "Einstiegshilfe" or "Erhaltungsereignis_AbwasserbauwerkAssoc"))
            if (bauwerke.Contains(q.Referenzen.GetValueOrDefault("AbwasserbauwerkRef", ""))) Hole(q.Kennung);
        foreach (var q in quellen.Values.Where(q => DssEinbautenZuordnung.Elternrolle(q.Klasse) is not null))
            if (objekte.ContainsKey(q.Referenzen.GetValueOrDefault(DssEinbautenZuordnung.Elternrolle(q.Klasse)!, ""))) Hole(q.Kennung);
        var ereignisse = objekte.Values.Where(o => o.Klasse == "Unterhalt").Select(o => o.Tid).ToHashSet();
        foreach (var q in quellen.Values.Where(q => q.Klasse == "Erhaltungsereignis_Ausfuehrende_FirmaAssoc"))
            if (ereignisse.Contains(q.Referenzen.GetValueOrDefault("Erhaltungsereignis_Ausfuehrende_FirmaAssocRef", ""))) Hole(q.Kennung);

        var kontext = new DssExportBearbeitung(projekt, objekte, roots, quellen, hinweise, mitZusatzangaben);
        kontext.Uebernehme();
        // 0..1 Firma ist in INTERLIS 2.3 am Ereignis eingebettet. Ältere Belege
        // mit separatem Link werden beim Neu-Schreiben in die Normschreibweise überführt.
        foreach (var assoc in objekte.Values.Where(o => o.Klasse == "Erhaltungsereignis_Ausfuehrende_FirmaAssoc").ToArray())
        {
            if (assoc.Werte.Count > 0 || assoc.Strukturen.Count > 0) throw new InvalidOperationException("DSS: Nicht zugeordnete Angaben an der Firmenbeziehung.");
            var ereignisId = assoc.Refs.GetValueOrDefault("Erhaltungsereignis_Ausfuehrende_FirmaAssocRef", "");
            var firma = assoc.Refs.GetValueOrDefault("Ausfuehrende_FirmaRef", "");
            if (!objekte.TryGetValue(ereignisId, out var ereignis) || ereignis.Klasse != "Unterhalt") throw new InvalidOperationException("DSS: Firmenbeziehung ohne eindeutiges Ereignis.");
            if (ereignis.Refs.TryGetValue("Ausfuehrende_FirmaRef", out var vorher) && vorher != firma) throw new InvalidOperationException("DSS: Mehrere ausführende Firmen für dasselbe Ereignis.");
            ereignis.Refs["Ausfuehrende_FirmaRef"] = firma;
            objekte.Remove(assoc.Tid);
        }
        DssExportPruefung.Pruefe(objekte, hinweise);
        foreach (var q in quellen.Values.Where(q => !objekte.ContainsKey(q.Kennung)
            && q.Klasse != "Erhaltungsereignis_Ausfuehrende_FirmaAssoc"))
            hinweise.Add($"Quellobjekt {q.Klasse} «{q.Werte.GetValueOrDefault("Bezeichnung", q.Kennung)}» ({q.Kennung}) fehlt in der XTF: gehört nicht zum exportierten Objektverbund.");
        hinweise.Add($"{objekte.Values.Sum(o => o.Werte.Count)} DSS-Feldwerte; {objekte.Values.Count(o => o.Klasse == "Deckel")} Deckel; {objekte.Values.Count(o => o.Klasse == "Unterhalt")} Unterhalts-/Sanierungsereignisse.");
        var plan = new XtfNeuPlan(objekte.Values.Select(o => o.Fertig()).ToArray(), hinweise,
            neu.Haltungen + importierteH.Count, neu.Schaechte + importierteS.Count, Dss: true);
        return mitZusatzangaben ? DssProjektAngaben.Ergaenze(plan, projekt, roots.ToDictionary(p => p.Key, p => p.Value.Tid)) : plan;

        DssExportObjekt? Hole(string? id, string? erwartet = null)
        {
            if (id is null || !quellen.TryGetValue(id, out var q)) return null;
            if (erwartet is not null && q.Klasse != erwartet) throw new InvalidOperationException($"DSS: Kennung {id} gehört zu {q.Klasse}, erwartet {erwartet}.");
            if (objekte.TryGetValue(id, out var alt)) return alt;
            if (DssExportSchema.Felder(q.Klasse) is null && !DssExportPruefung.Associationen.ContainsKey(q.Klasse))
                throw new InvalidOperationException($"DSS: Die referenzierte Klasse {q.Klasse} ist noch nicht im Exportvertrag enthalten ({id}).");
            var o = DssExportObjekt.Aus(q);
            if (mitZusatzangaben) DssQuellabweichungen.Trenne(o, hinweise);
            objekte.Add(id, o);
            // Ausschliesslich fehlende Quellnamen ergänzen, bevor aktuelle Eingaben angewendet werden.
            if (DssExportSchema.Felder(q.Klasse)?.GetValueOrDefault("Bezeichnung")?.Required == true && string.IsNullOrEmpty(o.Werte.GetValueOrDefault("Bezeichnung")))
            {
                o.Werte["Bezeichnung"] = o.Tid;
                hinweise.Add($"{o.Klasse} {o.Tid}: GeoShop liefert keine Pflichtbezeichnung; Originalkennung als technische Bezeichnung verwendet.");
            }
            foreach (var ziel in q.Referenzen.Values) Hole(ziel);
            return o;
        }
    }

    private static void PruefeBauwerkskennung(string? gespeichert, DssExportObjekt o)
    {
        if (!string.IsNullOrWhiteSpace(gespeichert) && o.Refs.GetValueOrDefault("AbwasserbauwerkRef") != gespeichert)
            throw new InvalidOperationException($"DSS: {o.Klasse} {o.Tid}: Bauwerkskennung {gespeichert} widerspricht dem Originalverweis {o.Refs.GetValueOrDefault("AbwasserbauwerkRef", "(leer)")}. Bitte GeoShop-Zuordnung klären; keine Ersatzkennung erzeugt.");
    }

    internal static Dictionary<string, ObjektQuellbeleg> Quellen(Project projekt)
    {
        var result = new Dictionary<string, ObjektQuellbeleg>(StringComparer.Ordinal);
        foreach (var gruppe in projekt.Objektakten.SelectMany(a => a.Quellen).Where(q => q.System == "GeoShop-XTF"
            && (q.Modell == DssExportSchema.Modell || q.Modell.StartsWith("SIA405_ABWASSER_2020", StringComparison.Ordinal) || q.Klasse == "Organisation")).GroupBy(q => q.Kennung, StringComparer.Ordinal))
        {
            var aktuell = gruppe.OrderByDescending(q => q.ImportiertUtc).First();
            foreach (var q in gruppe.Where(q => q.ImportiertUtc == aktuell.ImportiertUtc))
                if (q.Klasse != aktuell.Klasse || Signatur(q) != Signatur(aktuell))
                    throw new InvalidOperationException($"DSS: widersprüchliche Quellbelege für {gruppe.Key}. Bitte den GeoShop-Abgleich erneuern.");
            result.Add(gruppe.Key, aktuell);
        }
        return result;
    }
    private static string Signatur(ObjektQuellbeleg q) => JsonSerializer.Serialize(new[]
        { q.Werte.OrderBy(p => p.Key).ToArray(), q.Referenzen.OrderBy(p => p.Key).ToArray(), q.Strukturen.OrderBy(p => p.Key).ToArray() });
}
