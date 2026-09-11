using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Belegte Listen aus eigenen Objekten und Quellbeziehungen; keine Kopie editierbarer Werte.</summary>
public static class ObjektaktenListen
{
    /// <summary>Der technische Schluessel des WebGIS. Er hat in SewerStudio kein Feld und
    /// wird nicht angezeigt.</summary>
    public static bool IstTechnischeSpalte(string titel)
        => titel.Equals("GlobalId", StringComparison.OrdinalIgnoreCase);

    /// <summary>Sichtbare Spalten einer Liste.</summary>
    public static IReadOnlyList<string> Spalten(ObjektUnterliste liste)
        => liste.Spalten.Where(s => !string.IsNullOrWhiteSpace(s) && !IstTechnischeSpalte(s)).ToArray();

    /// <summary>Das Feld der Zielobjektart, das eine Spalte fuellt. Zugeordnet wird ueber die
    /// Beschriftung; ohne Treffer bleibt die Spalte leer statt einen fremden Wert zu zeigen.</summary>
    public static ObjektFeldDefinition? Spaltenfeld(ObjektUnterliste liste, string spalte)
        => FieldCatalog.Objektfelder.Felder.FirstOrDefault(
            f => f.Art == liste.ZeigtAufObjektart && f.Label == spalte);

    /// <summary>Eigene Akten, die in dieser Liste stehen. Leer, wenn die Liste auf einen
    /// vorhandenen Projektdatensatz zeigt.</summary>
    public static IEnumerable<ObjektAkte> Akten(ObjektaktenBearbeitung b, ObjektAkte a, ObjektUnterliste liste)
    {
        if (!liste.EigeneObjektart || liste.ZeigtAufObjektart.Length == 0) return [];
        return b.Verbund.Where(s => s.Id != b.WurzelId && s.Art == liste.ZeigtAufObjektart
            && (liste.Label != "Hauptdeckel" || s.Id == a.HauptdeckelId));
    }

    public static IEnumerable<IReadOnlyDictionary<string, string>> Zeilen(ObjektaktenBearbeitung b, ObjektAkte a, ObjektUnterliste liste)
    {
        if (a.Unterlisten.TryGetValue(liste.Id, out var gespeichert))
            foreach (var zeile in gespeichert) yield return zeile;
        if (liste.Label == "Unterhaltsmassnahmen")
            foreach (var q in AktuelleQuellen(a).Where(q => q.Klasse == "Unterhalt")) yield return q.Werte;
        if (liste.Label == "Bauwerksteile")
            foreach (var q in AktuelleQuellen(a).Where(q => q.Klasse == "Einstiegshilfe")) yield return q.Werte;
        if (a.Art == "schacht" && liste.Label is "Einläufe" or "Ausläufe")
        {
            var knoten = b.Projekt.SchaechteData.Single(s => s.Id == a.Id).Geonis?.Knoten;
            var quellen = AktuelleQuellen(a).ToArray();
            var rolle = liste.Label == "Einläufe" ? "nachHaltungspunktRef" : "vonHaltungspunktRef";
            foreach (var punkt in quellen.Where(q => q.Klasse == "Haltungspunkt" && q.Referenzen.GetValueOrDefault("AbwassernetzelementRef") == knoten))
                foreach (var h in quellen.Where(q => q.Klasse == "Haltung" && q.Referenzen.GetValueOrDefault(rolle) == punkt.Kennung))
                    yield return new Dictionary<string, string>
                    {
                        ["Haltung"] = h.Werte.GetValueOrDefault("Bezeichnung", ""), ["Punkt"] = punkt.Kennung,
                        ["Kote"] = punkt.Werte.GetValueOrDefault("Kote", ""), ["Lichte Höhe"] = h.Werte.GetValueOrDefault("Lichte_Hoehe", "")
                    };
        }
    }

    private static IEnumerable<ObjektQuellbeleg> AktuelleQuellen(ObjektAkte a) => a.Quellen
        .GroupBy(q => (q.Modell, q.Klasse, q.Kennung)).Select(g => g.OrderBy(q => q.ImportiertUtc).Last());
}
