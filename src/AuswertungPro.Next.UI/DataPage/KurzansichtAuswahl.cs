using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Vorhandene Begriffe der Objektmaske erkennen, ohne neue Normwerte zu erfinden.</summary>
internal static class KurzansichtAuswahl
{
    internal static bool AusObjektmaske(string feld, BauteilArt art, string wert)
    {
        var key = art == BauteilArt.Schacht ? SchaechteColumnPolicy.ResolveOptionField(feld) ?? feld : feld;
        if (art == BauteilArt.Schacht && key == FieldKeys.ShaftShape
            && SchachtformVokabular.Auswahl.Contains(SchachtformVokabular.Normalisieren(wert))) return true;
        // Die Funktion bleibt von der Bauwerksart abhaengig; deren Filter nicht umgehen.
        if (art == BauteilArt.Schacht && key == "Funktion") return false;
        var definitionen = FieldCatalog.Objektfelder.Felder.Where(f => f.Art == (art == BauteilArt.Schacht ? "schacht" : "haltung")
            && f.Speicherfeld is { } speicher && SchachtFeldnamen.Falte(speicher) == SchachtFeldnamen.Falte(key));
        return definitionen.Any(f => new[] { f.KatalogId, f.KatalogIdJeEltern }.Where(id => id is not null)
            .Any(id => FieldCatalog.Objektfelder.Auswahl(id)?.Eintraege.Any(e =>
                string.Equals(e.Label, wert, StringComparison.OrdinalIgnoreCase)) == true));
    }
}
