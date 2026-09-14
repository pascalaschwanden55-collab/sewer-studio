using System.Text.Json;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Verlustfreie Sachangaben neben dem unveränderten Normmodell. Kein JSON in DSS-Feldern.</summary>
internal static class DssProjektAngaben
{
    internal const string Feld = "Erfasste_Angaben";

    internal static XtfNeuPlan Ergaenze(XtfNeuPlan plan, Project p, IReadOnlyDictionary<Guid, string> roots)
    {
        var objekte = plan.Objekte.ToList();
        var ids = new XtfNeuKennungen(p.Id.ToString("N"));
        var eintraege = new Dictionary<string, List<object>>(StringComparer.Ordinal);
        var akten = p.Objektakten.ToDictionary(a => a.Id);
        foreach (var h in p.Data) Erfasse(h.Id, "haltung", h.Fields, h.FieldMeta);
        foreach (var s in p.SchaechteData) Erfasse(s.Id, "schacht", s.Fields, s.FieldMeta);
        foreach (var a in p.Objektakten.Where(a => !roots.ContainsKey(a.Id))) Erfasse(a.Id, a.Art, new Dictionary<string, string>(), new Dictionary<string, FieldMetadata>());
        foreach (var q in DssExportPlanBuilder.Quellen(p).Values.Where(q => plan.Objekte.Any(o => o.Tid == q.Kennung)))
        {
            var abweichungen = q.Werte.Where(w => DssQuellabweichungen.IstAbweichung(q.Klasse, w.Key, w.Value))
                .OrderBy(w => w.Key, StringComparer.Ordinal).ToDictionary(w => w.Key, w => w.Value);
            if (abweichungen.Count == 0) continue;
            if (!eintraege.TryGetValue(q.Kennung, out var liste)) eintraege[q.Kennung] = liste = [];
            liste.Add(new { Klasse = q.Klasse, Quellabweichungen = abweichungen });
        }
        foreach (var (tid, daten) in eintraege.OrderBy(p => p.Key, StringComparer.Ordinal))
            objekte.Add(new("Zusatzangabe", ids.Fuer("Zusatzangabe", tid + "|" + Feld),
                [new("ObjektTid", tid), new("Feld", Feld), new("Wert", JsonSerializer.Serialize(new { Version = 1, Objekte = daten }))], [], ImTopicZusatz: true));
        var hinweise = plan.Hinweise.Select(h => h.Contains("fehlt in der XTF", StringComparison.Ordinal)
            ? h + " Erfasste Eingaben sind zusätzlich unverändert unter Erfasste_Angaben mitgeliefert; dafür ist eine eigene Zuordnung im Zielsystem nötig." : h).ToList();
        hinweise.Add($"{eintraege.Count} Pakete Erfasste_Angaben im separaten Modell {XtfZusatzangaben.Modell}: aktuelle Projektfelder, Objektfelder mit Originalcodes, bewusstes Leeren und Unterlisten. Normfelder werden weiterhin gemäss ILI geschrieben. Zusatzangaben sind keine SIA405-Attribute.");
        return plan with { Objekte = objekte, Hinweise = hinweise };

        void Erfasse(Guid id, string art, IReadOnlyDictionary<string, string> felder, IReadOnlyDictionary<string, FieldMetadata> meta)
        {
            akten.TryGetValue(id, out var akte);
            var werte = felder.Where(f => !IstDateipfad(f.Key) && (f.Value.Length > 0 || meta.GetValueOrDefault(f.Key)?.UserEdited == true))
                .OrderBy(f => f.Key, StringComparer.Ordinal).ToDictionary(f => f.Key,
                    f => new { Wert = f.Value, VonHand = meta.GetValueOrDefault(f.Key)?.UserEdited == true,
                        GeaendertUtc = meta.GetValueOrDefault(f.Key)?.LastUpdatedUtc });
            var objektwerte = new SortedDictionary<string, object>(StringComparer.Ordinal);
            if (akte is not null) foreach (var (key, wert) in akte.Werte.OrderBy(w => w.Key, StringComparer.Ordinal))
            {
                if (wert.Text.Length == 0 && !wert.VonHand) continue;
                var feld = FieldCatalog.Objektfelder.Felder.FirstOrDefault(f => f.Id == key)?.Speicherfeld;
                if (feld is not null && art == "schacht") feld = SchachtFeldnamen.Feld(p.SchaechteData.Single(s => s.Id == id), feld);
                // Ein alter Anzeigetext darf den aktuellen Recordwert niemals ersetzen.
                var aktuell = feld is null ? null : felder.GetValueOrDefault(feld, "");
                var synchron = aktuell is null || aktuell == wert.Bestandswert;
                objektwerte[key] = new { Wert = synchron ? wert.Text : aktuell, wert.VonHand, wert.GeaendertUtc,
                    KatalogId = synchron ? wert.KatalogId : null, Originalcode = synchron ? wert.Originalcode : null, wert.Zusatzdaten };
            }
            if (werte.Count == 0 && objektwerte.Count == 0 && akte?.Unterlisten.Count is not > 0) return;
            var tid = roots.GetValueOrDefault(id);
            if (tid is null && akte is not null)
            {
                var klasse = DssObjektarten.Klasse(art);
                var original = akte.Quellen.Where(q => !q.IstLokaleKennung && (q.Klasse == klasse || DssEinbautenZuordnung.Art(q.Klasse) == art)).Select(q => q.Kennung).Distinct().ToArray();
                tid = original.Length == 1 ? original[0] : original.Length == 0 && klasse is "Deckel" or "Unterhalt" ? ids.Fuer(klasse, id.ToString("N")) : null;
            }
            if (tid is null || !plan.Objekte.Any(o => o.Tid == tid && !o.OhneTid))
                throw new InvalidOperationException($"DSS: Erfasste Angaben an {art} {id} haben kein eindeutiges Exportobjekt. Keine unvollständige XTF geschrieben.");
            if (!eintraege.TryGetValue(tid, out var liste)) eintraege[tid] = liste = [];
            liste.Add(new { Art = art, Felder = werte, Objektfelder = objektwerte, Unterlisten = akte?.Unterlisten,
                Bezuege = akte?.Bezuege.Select(id => roots.GetValueOrDefault(id)).ToArray(), WeitereAngaben = akte?.Zusatzdaten,
                HauptdeckelTid = akte?.HauptdeckelId is { } deckel && akten.TryGetValue(deckel, out var d)
                    ? d.Quellen.SingleOrDefault(q => q.Klasse == "Deckel")?.Kennung ?? ids.Fuer("Deckel", deckel.ToString("N")) : null });
        }
    }

    private static bool IstDateipfad(string key) => key is FieldKeys.PdfPath or "Video_Path" or "Link" or "ImportBezeichnung";
}
