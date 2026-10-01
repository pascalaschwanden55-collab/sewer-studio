using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Keine als vollstaendig bezeichnete Datei, wenn ganze erfasste Akten fehlen.</summary>
internal static class DssObjektaktenAbdeckung
{
    internal static void Pruefe(Project projekt)
    {
        var haltungen = projekt.Data.Select(h => h.Id).ToHashSet();
        var schaechte = projekt.SchaechteData.Select(s => s.Id).ToHashSet();
        var roots = haltungen.Concat(schaechte).ToHashSet();
        var fehlt = new List<string>();
        foreach (var a in projekt.Objektakten)
        {
            string? grund = DssObjektarten.Klasse(a.Art) is null
                ? "Objektart ist noch nicht an den DSS-Export angebunden"
                : a.Art == "haltung" ? haltungen.Contains(a.Id) ? null : "Haltung fehlt im Projekt"
                : a.Art == "schacht" ? schaechte.Contains(a.Id) ? null : "Schacht fehlt im Projekt"
                : a.Bezuege.Count == 0 || a.Bezuege.Any(id => !roots.Contains(id)) ? "Zuordnung zu einem vorhandenen Projektobjekt fehlt"
                : DssEinbautenZuordnung.IstAkte(a.Art) && a.Quellen.Where(q => DssEinbautenZuordnung.Art(q.Klasse) == a.Art && !q.IstLokaleKennung)
                    .Select(q => q.Kennung).Distinct().Count() != 1 ? "Original-Einbau fehlt oder ist mehrdeutig; Neuanlage noch nicht angebunden" : null;
            if (grund is null) continue;
            var felder = a.Werte.Where(w => w.Value.Text.Length > 0 || w.Value.VonHand)
                .Select(w => FieldCatalog.Objektfelder.Felder.FirstOrDefault(f => f.Id == w.Key)?.Label ?? w.Key);
            fehlt.Add($"{DssObjektarten.Bezeichnung(a)}: nicht geliefert – {grund}. Erfasste Felder: {string.Join(", ", felder)}.");
        }
        if (fehlt.Count > 0)
            throw new InvalidOperationException($"DSS: {fehlt.Count} Objektakten können nicht vollständig geliefert werden. Keine XTF erstellt.\n"
                + string.Join("\n", fehlt));
    }
}
