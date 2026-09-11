using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

public static class ObjektaktenBestandsfelder
{
    public static IEnumerable<ObjektFeldDefinition> Fuer(ObjektaktenBearbeitung b, ObjektAkte a)
    {
        var katalog = FieldCatalog.Objektfelder.Felder.Where(f => f.Art == a.Art).ToArray();
        foreach (var f in katalog) yield return f;
        if (a.Id != b.WurzelId) yield break;
        var s = b.Art == "schacht" ? b.Projekt.SchaechteData.Single(r => r.Id == a.Id) : null;
        var keys = s is not null ? s.Fields.Keys.AsEnumerable() : b.Projekt.Data.Single(r => r.Id == a.Id).Fields.Keys;
        var belegt = katalog.Where(f => f.Speicherfeld is not null)
            .Select(f => s is null ? f.Speicherfeld! : SchachtFeldnamen.Feld(s, f.Speicherfeld!)).ToHashSet();
        foreach (var key in keys.Where(k => !belegt.Contains(k)))
        {
            var def = FieldCatalog.Get(key);
            yield return new ObjektFeldDefinition
            {
                Id = a.Art + ".bestand." + key, Art = a.Art, Label = def.Label, Speicherfeld = key,
                Thema = "SewerStudio", Gruppe = "Bisherige Angaben", Belegstatus = "bestehendes Projektfeld",
                // Namen, Pfade und berechnete Spalten werden über ihre bisherigen Abläufe geändert.
                NurLesen = !FieldCatalog.ColumnOrder.Contains(key)
                    || key is "NR" or "Haltungsname" or "Schachtnummer" or "Link"
            };
        }
    }
}
