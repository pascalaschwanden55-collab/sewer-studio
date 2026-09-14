using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Feldaufträge am geprüften Originalverbund; Pflichtkontext bleibt normgerecht vollständig.</summary>
public static class DssAenderungsPlanBuilder
{
    public static XtfNeuPlan Build(XtfNeuPlan voll, Project projekt)
    {
        if (!voll.Dss) throw new InvalidOperationException("DSS-Änderungen benötigen einen geprüften DSS-Plan.");
        var quellen = DssExportPlanBuilder.Quellen(projekt);
        var auftraege = new Dictionary<(string Tid, string Feld), DateTime>();
        var zeit = DateTime.UtcNow;
        var ids = new XtfNeuKennungen(projekt.Id.ToString("N"));
        foreach (var o in voll.Objekte.Where(o => !o.ImTopicZusatz && !o.OhneTid))
        {
            quellen.TryGetValue(o.Tid, out var q);
            if (q is not null && q.Klasse != o.Klasse)
                throw new InvalidOperationException($"DSS: Originalkennung {o.Tid} gehört zu {q.Klasse}, nicht zu {o.Klasse}.");
            var alt = q is null ? new Dictionary<string, string>() : Werte(q);
            if (o.Klasse == "Unterhalt")
            {
                var firmen = quellen.Values.Where(q => q.Klasse == "Erhaltungsereignis_Ausfuehrende_FirmaAssoc"
                    && q.Referenzen.GetValueOrDefault("Erhaltungsereignis_Ausfuehrende_FirmaAssocRef") == o.Tid)
                    .Select(q => q.Referenzen.GetValueOrDefault("Ausfuehrende_FirmaRef")).Where(t => t is not null).Distinct().ToArray();
                if (firmen.Length == 1) alt["Ausfuehrende_FirmaRef"] = firmen[0]!;
            }
            var neu = Werte(o);
            foreach (var feld in alt.Keys.Union(neu.Keys, StringComparer.Ordinal).Where(f => f != "Letzte_Aenderung"))
                if (alt.GetValueOrDefault(feld) != neu.GetValueOrDefault(feld)
                    && !(q is not null && feld == "Bezeichnung" && !alt.ContainsKey(feld) && neu.GetValueOrDefault(feld) == o.Tid))
                    auftraege[(o.Tid, feld)] = zeit;
        }
        const string beziehung = "Erhaltungsereignis_AbwasserbauwerkAssoc";
        var neuBezuege = voll.Objekte.Where(o => o.Klasse == beziehung)
            .GroupBy(o => o.Verweise.Single(v => v.Name == beziehung + "Ref").ZielTid)
            .ToDictionary(g => g.Key, g => g.Select(o => o.Verweise.Single(v => v.Name == "AbwasserbauwerkRef").ZielTid).ToHashSet(StringComparer.Ordinal));
        foreach (var (tid, neu) in neuBezuege)
        {
            var alt = quellen.Values.Where(q => q.Klasse == beziehung && q.Referenzen.GetValueOrDefault(beziehung + "Ref") == tid)
                .Select(q => q.Referenzen["AbwasserbauwerkRef"]).ToHashSet(StringComparer.Ordinal);
            if (!alt.SetEquals(neu)) auftraege[(tid, "Beziehung:" + beziehung)] = zeit;
        }
        var objekte = voll.Objekte.Where(o => !o.ImTopicZusatz).ToList();
        foreach (var zusatz in voll.Objekte.Where(o => o.ImTopicZusatz && o.Klasse == "Zusatzangabe"))
        {
            var tid = zusatz.Felder.Single(f => f.Key == "ObjektTid").Value;
            var feld = zusatz.Felder.Single(f => f.Key == "Feld").Value;
            // Das Paket erhält auch bewusst geleerte Werte; ein Export quittiert sie nicht.
            if (feld == DssProjektAngaben.Feld)
            {
                using var json = JsonDocument.Parse(zusatz.Felder.Single(f => f.Key == "Wert").Value);
                objekte.Add(zusatz);
                if (!HatHandwert(json.RootElement) && !auftraege.Keys.Any(k => k.Tid == tid)) continue;
            }
            else continue; // Doppelte Kurzwerte werden durch das vollständige Eingabepaket abgedeckt.
            auftraege[(tid, "Zusatz:" + feld)] = zeit;
        }
        if (auftraege.Count == 0) return new([], voll.Hinweise, 0, 0, true, true);
        foreach (var ((tid, feld), datum) in auftraege.OrderBy(p => p.Key.Tid, StringComparer.Ordinal).ThenBy(p => p.Key.Feld, StringComparer.Ordinal))
            objekte.Add(new("Aenderung", ids.Fuer("Aenderung", tid + "|" + feld),
                [new("ObjektTid", tid), new("Feld", feld), new("GeaendertAm", datum.ToString("O", CultureInfo.InvariantCulture))], [], ImTopicZusatz: true));
        var hinweise = voll.Hinweise.Append($"Änderungslieferung: {auftraege.Count} Feldaufträge an Original-TIDs. Nur Aenderung-Einträge sind Schreibaufträge; fehlender optionaler Wert bei vorhandenem Auftrag bedeutet Leeren (gegebenenfalls mit genauerem Wert im Zusatz). Beziehung:{beziehung} bezeichnet den vollständigen Bauwerksbezug des genannten Ereignisses über die gleichnamigen Normassoziationen. Alle übrigen Normobjekte dienen als vollständiger Bezugskontext. Zusatz:Erfasste_Angaben enthält separat zuzuordnende Eingaben, keine erfundenen DSS-Attribute. GeaendertAm ist der Zeitpunkt der Auftragserzeugung; die gespeicherten Bearbeitungszeiten stehen unverändert im Eingabepaket.").ToArray();
        return voll with { Objekte = objekte, Hinweise = hinweise, NurAenderungen = true };
    }

    private static Dictionary<string, string> Werte(ObjektQuellbeleg q) =>
        Werte(new XtfNeuObjekt(q.Klasse, q.Kennung, q.Werte.ToArray(), q.Referenzen.Select(p => new XtfNeuVerweis(p.Key, p.Value)).ToArray(), Strukturen: q.Strukturen));
    private static Dictionary<string, string> Werte(XtfNeuObjekt o)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in o.Felder)
            if (!DssQuellabweichungen.IstAbweichung(o.Klasse, k, v) && DssExportSchema.Normalisiere(o.Klasse, k, v) is { } norm) result.Add(k, norm);
        foreach (var v in o.Verweise) result.Add(v.Name, v.ZielTid);
        foreach (var (k, v) in o.Strukturen ?? new Dictionary<string, string>()) result.Add(k, XElement.Parse(v).ToString(SaveOptions.DisableFormatting));
        if (o.Geometrie is not null) result[o.Geometrie.Feldname] = JsonSerializer.Serialize(o.Geometrie);
        return result;
    }
    private static bool HatHandwert(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().Any(p => p.Name == "VonHand" && p.Value.ValueKind == JsonValueKind.True
            || p.Name == "Unterlisten" && p.Value.ValueKind == JsonValueKind.Object && p.Value.EnumerateObject().Any()
            || HatHandwert(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Any(HatHandwert),
        _ => false
    };
}
