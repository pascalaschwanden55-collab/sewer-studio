namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die Organisationsliste des WebGIS (Eigentuemer, Betreiber, Buero …) samt Organisationstyp, «so wie es im
/// WebGIS ist» (Entscheid Pascal 24.09.2026). Quelle ist die Liste der Haltungsmaske im Objektaktenkatalog
/// (<c>haltung.owner</c>): Dort zeigt das WebGIS jeden Eintrag als «Name (Typ)», zum Beispiel
/// «AWU_von_privat (Abwasserverband)»; die Schachtmaske zeigt nur den Namen. Gefunden wird beides.
/// Kein Treffer heisst null — dann entscheiden die bisherigen Regeln in <see cref="EigentumVokabular"/>.
/// </summary>
public static class WebGisOrganisationen
{
    /// <param name="Name">Name der Organisation im WebGIS, ohne angehaengten Typ.</param>
    /// <param name="WebGisTyp">Typ, wie ihn das WebGIS nennt («Genossenschaft/Kooperation»).</param>
    /// <param name="Organisationstyp">Derselbe Typ als SIA405-Wert; null bei «Unbekannt» und unbekannten Typen.</param>
    public sealed record Organisation(string Name, string WebGisTyp, string? Organisationstyp);

    // Die WebGIS-Typen und ihr SIA405-Wert (SIA405_Base_Abwasser, Organisationstyp). «Unbekannt» hat dort kein
    // Gegenstueck; ein anderer Typ ohne Eintrag ergibt ebenfalls keinen — nie raten.
    private static readonly Dictionary<string, string> NachSia405 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Abwasserverband"] = "Abwasserverband",
        ["Bund"] = "Bund",
        ["Kanton"] = "Kanton",
        ["Gemeinde"] = "Gemeinde",
        ["Privat"] = "Privat",
        ["Genossenschaft/Kooperation"] = "Genossenschaft_Korporation",
    };

    private static readonly Lazy<IReadOnlyList<Organisation>> Liste = new(Lade);

    public static IReadOnlyList<Organisation> Alle => Liste.Value;

    /// <summary>Die WebGIS-Organisation zu «Name» oder «Name (Typ)», gross/klein egal; sonst null.</summary>
    public static Organisation? Finde(string? text)
    {
        var wert = (text ?? "").Trim();
        if (wert.Length == 0) return null;
        return Alle.FirstOrDefault(o => string.Equals(o.Name, wert, StringComparison.OrdinalIgnoreCase)
            || string.Equals($"{o.Name} ({o.WebGisTyp})", wert, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<Organisation> Lade()
    {
        var katalog = FieldCatalog.Objektfelder;
        var eintraege = katalog.Auswahl(katalog.Feld("haltung.owner").KatalogId)?.Eintraege ?? [];
        var liste = new List<Organisation>();
        foreach (var eintrag in eintraege)
        {
            var label = (eintrag.Label ?? "").Trim();
            var klammer = label.LastIndexOf(" (", StringComparison.Ordinal);
            if (klammer <= 0 || !label.EndsWith(')')) continue; // ohne Typ kein Eintrag dieser Liste
            var name = label[..klammer].Trim();
            var typ = label[(klammer + 2)..^1].Trim();
            liste.Add(new Organisation(name, typ, NachSia405.GetValueOrDefault(typ)));
        }
        return liste;
    }
}
