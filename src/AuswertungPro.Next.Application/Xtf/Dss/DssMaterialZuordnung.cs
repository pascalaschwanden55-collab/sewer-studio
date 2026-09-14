using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Explizite Schreibweisen des WebGIS gegen DSS_2020_1_LV95. Keine Zuordnung allein aus der Materialgruppe.</summary>
public static class DssMaterialZuordnung
{
    private static readonly IReadOnlyDictionary<string, string?> Haltung = new Dictionary<string, string?>
    {
        ["0"] = "unbekannt", ["101"] = "Beton_unbekannt", ["102"] = null, ["103"] = null,
        ["104"] = null, ["105"] = null, ["106"] = "Beton_Ortsbeton", ["107"] = null,
        ["108"] = null, ["109"] = null, ["144"] = "Beton_Normalbeton", ["146"] = "Beton_Pressrohrbeton",
        ["147"] = "Beton_Spezialbeton", ["1000"] = null, ["1003"] = null,
        ["142"] = "Stahl", ["127"] = null, ["128"] = null, ["148"] = "Stahl_rostfrei",
        ["118"] = "Kunststoff_unbekannt", ["117"] = null, ["120"] = null,
        ["121"] = "Kunststoff_Polyvinilchlorid", ["122"] = null, ["123"] = "Kunststoff_Epoxydharz",
        ["124"] = "Kunststoff_Polypropylen", ["133"] = "Kunststoff_Polyethylen", ["143"] = "Kunststoff_Hartpolyethylen",
        ["145"] = null, ["1001"] = null, ["113"] = null, ["114"] = "Guss_Grauguss", ["115"] = "Guss_duktil",
        ["116"] = null, ["131"] = null, ["110"] = "Faserzement", ["111"] = "Asbestzement",
        ["112"] = "Gebrannte_Steine", ["129"] = "Steinzeug", ["130"] = "Ton", ["132"] = "Zement",
        ["149"] = "andere", ["1002"] = null
    };
    private static readonly IReadOnlyDictionary<string, string?> Schacht = new Dictionary<string, string?>
    {
        // WebGIS-Code 104 = Beton, Fertigteil. Normschacht.Material kennt Beton,
        // aber keine Herstellungsart. Das Detail bleibt in Erfasste_Angaben erhalten.
        ["0"] = "unbekannt", ["101"] = "Beton", ["102"] = null, ["103"] = null, ["104"] = "Beton",
        ["105"] = null, ["106"] = "Beton", ["107"] = null, ["108"] = null, ["109"] = null, ["110"] = null,
        ["122"] = null, ["123"] = null, ["124"] = null, ["119"] = "Kunststoff", ["120"] = null,
        ["121"] = null, ["115"] = null, ["116"] = null, ["117"] = null, ["118"] = null,
        ["111"] = null, ["112"] = null, ["113"] = null, ["114"] = null
    };

    public static IReadOnlyList<DssMaterialEntscheid> Alle() => new[] { "haltung.material", "schacht.materialdetail" }
        .SelectMany(id => FieldCatalog.Objektfelder.Auswahl(FieldCatalog.Objektfelder.Feld(id).KatalogIdJeEltern)!.Eintraege
            .Select(e => Entscheide(id, e))).ToArray();

    public static DssMaterialEntscheid? Fuer(string feldId, string text)
    {
        if (feldId is not ("haltung.material" or "schacht.materialdetail")) return null;
        var f = FieldCatalog.Objektfelder.Feld(feldId);
        var e = FieldCatalog.Objektfelder.Auswahl(f.KatalogIdJeEltern)!.Eintraege.SingleOrDefault(e => e.Label == text);
        return e is null ? null : Entscheide(feldId, e);
    }

    private static DssMaterialEntscheid Entscheide(string feldId, ObjektAuswahl e)
    {
        var map = feldId == "haltung.material" ? Haltung : Schacht;
        var erfasst = e.OriginalCode is not null && map.ContainsKey(e.OriginalCode);
        var norm = erfasst ? map[e.OriginalCode!] : null;
        return new(feldId, e.OriginalCode ?? "", e.Label, norm, erfasst,
            norm is null ? "Fachliche Zuordnung offen: Das Normmodell enthält diesen Detailbegriff nicht. Export gesperrt; bitte einen belegten Normwert fachlich festlegen."
            : feldId == "schacht.materialdetail" && e.OriginalCode == "106"
                ? "DSS liefert Beton. Ortsbeton bleibt zusätzlich im Projekt erhalten (bestehende Schachtzuordnung)."
                : "Eindeutige Schreibweise; gegen die Materialwerte des gelieferten DSS-Modells geprüft.");
    }
}

public sealed record DssMaterialEntscheid(string FeldId, string Code, string Auswahl, string? Normwert, bool Erfasst, string Hinweis);
