using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Nur belegte Beziehungen. WebGIS-Gruppen und Anzeigehilfen sind keine Normattribute.</summary>
internal static class DssFeldZuordnung
{
    private static readonly Dictionary<string, string> Ergaenzungen = new()
    {
        ["haltung.remarks"] = "Kanal.Bemerkung", ["schacht.bemerkung"] = "Normschacht.Bemerkung",
        ["schacht.bezeichnung"] = "Abwasserknoten.Bezeichnung",
        ["haltung.profile"] = "Rohrprofil.Profiltyp",
        ["haltung.fromlevel"] = "von.Kote", ["haltung.tolevel"] = "nach.Kote",
        ["haltung.fromheightaccuracy"] = "von.Hoehengenauigkeit", ["haltung.toheightaccuracy"] = "nach.Hoehengenauigkeit",
        ["haltung.fromoutlet"] = "von.Auslaufform", ["haltung.tooutlet"] = "nach.Auslaufform",
        ["haltung.fromclock"] = "von.Lage_Anschluss", ["haltung.toclock"] = "nach.Lage_Anschluss",
        ["haltung.planslope"] = "Haltung.Plangefaelle", ["haltung.friction"] = "Haltung.Reibungsbeiwert",
        ["haltung.roughness"] = "Haltung.Wandrauhigkeit", ["haltung.flowtime"] = "Haltung.Fliesszeit_Trockenwetter",
        ["haltung.hydroload"] = "Haltung.Hydr_Belastung_Ist", ["haltung.leakprotection"] = "Haltung.Leckschutz",
        ["haltung.stiffness"] = "Haltung.Ringsteifigkeit",
        ["haltung.pipelength"] = "Kanal.Rohrlaenge",
        ["schacht.hydraulische_geometrie"] = "Abwasserknoten.Hydr_GeometrieRef",
        ["sanierung.beginn"] = "Unterhalt.Zeitpunkt", ["sanierung.s_year"] = "Unterhalt.Zeitpunkt",
        ["sanierung.ausfuehrender"] = "Unterhalt.Ausfuehrender", ["sanierung.bemerkung"] = "Unterhalt.Bemerkung",
        ["sanierung.datengrundlage"] = "Unterhalt.Datengrundlage", ["sanierung.dauer"] = "Unterhalt.Dauer",
        ["sanierung.detaildaten"] = "Unterhalt.Detaildaten", ["sanierung.ergebnis"] = "Unterhalt.Ergebnis",
        ["sanierung.grund"] = "Unterhalt.Grund", ["sanierung.kosten"] = "Unterhalt.Kosten",
        ["unterhalt.bezeichnung"] = "Unterhalt.Bezeichnung", ["unterhalt.art"] = "Unterhalt.Art",
        ["unterhalt.status"] = "Unterhalt.Status", ["unterhalt.zeitpunkt"] = "Unterhalt.Zeitpunkt",
        ["unterhalt.ausfuehrender"] = "Unterhalt.Ausfuehrender", ["unterhalt.bemerkung"] = "Unterhalt.Bemerkung",
        ["unterhalt.datengrundlage"] = "Unterhalt.Datengrundlage", ["unterhalt.dauer_t"] = "Unterhalt.Dauer",
        ["unterhalt.detaildaten"] = "Unterhalt.Detaildaten", ["unterhalt.ergebnis"] = "Unterhalt.Ergebnis",
        ["unterhalt.grund"] = "Unterhalt.Grund", ["unterhalt.kosten"] = "Unterhalt.Kosten"
    };
    public static (string Klasse, string Attribut)? Ziel(ObjektFeldDefinition f, string? klasse = null)
    {
        if (klasse is not null && DssEinbautenZuordnung.Ziel(f, klasse) is { } einbau) return einbau;
        var text = Ergaenzungen.GetValueOrDefault(f.Id) ?? f.Exportziel;
        if (text is null) return null;
        var m = Regex.Match(text, @"^([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)(?: \(geerbt\))?$");
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : null;
    }
    public static string Normwert(string klasse, string attribut, string text)
    {
        if (attribut == "BaulicherZustand")
            text = text switch { "Nicht mehr funktionstüchtig (Z0)" => "Z0", "Starke Mängel (Z1)" => "Z1",
                "Mittlere Mängel (Z2)" => "Z2", "Leichte Mängel (Z3)" => "Z3", "Keine Mängel (Z4)" => "Z4", _ => text };
        if (attribut == "Signaluebermittlung" && text == "Senden, empfangen") text = "senden_empfangen";
        if (klasse == "Streichwehr" && attribut == "Wehr_Art")
            text = text switch { "Streichwehr, hochgezogen" => "hochgezogen", "Streichwehr, niedrig" => "niedrig", "Unbekannt" => "", _ => text };
        if (klasse == "Haltungspunkt" && attribut == "Hoehengenauigkeit")
            text = text switch { "> 6 cm" => "groesser_6cm", "+/- 1 cm" => "plusminus_1cm",
                "+/- 3 cm" => "plusminus_3cm", "+/- 6 cm" => "plusminus_6cm", _ => text };
        if (attribut == "Material" && DssMaterialZuordnung.Fuer(klasse == "Haltung" ? "haltung.material"
            : klasse == "Normschacht" ? "schacht.materialdetail" : "", text) is { } material)
            return material.Normwert ?? throw new InvalidOperationException($"DSS: {klasse}.Material „{text}“: {material.Hinweis}");
        if (klasse == "Unterhalt" && attribut == "Art")
            text = text switch { "Reparatur" => "Sanierung_Reparatur", "Renovierung" => "Sanierung_Renovierung", "Erneuerung" => "Sanierung_Erneuerung", _ => text };
        // Bereits korrekte Normtexte (auch unbekannt) unverändert lassen.
        try { return DssExportSchema.Normalisiere(klasse, attribut, text) ?? ""; }
        catch (InvalidOperationException)
        {
            // Bekannte SewerStudio-Kurztexte erst übersetzen. Ein weiterhin ungültiger
            // Wert bleibt erhalten und wird beim Setzen vom Schema-Prüfer abgewiesen.
            var norm = klasse == "Normschacht" ? XtfSchachtPlanBuilder.NachXtfWert(attribut, text)
                : XtfStammdatenPlanBuilder.NachXtfWert(attribut, text, DssExportSchema.Modell);
            return string.IsNullOrEmpty(norm) ? text : norm;
        }
    }

    public static string? UnterhaltAnzeige(ObjektFeldDefinition feld, string? wert)
        => Ziel(feld) is { Klasse: "Unterhalt" } ? KatalogAnzeige(feld, wert) : wert;

    public static string? KatalogAnzeige(ObjektFeldDefinition feld, string? wert, string? klasse = null,
        IEnumerable<ObjektAuswahl>? erlaubteEintraege = null)
    {
        if (wert is null || Ziel(feld, klasse) is not { } ziel
            || FieldCatalog.Objektfelder.Auswahl(feld.KatalogId) is not { } katalog) return wert;
        var kandidaten = (erlaubteEintraege ?? katalog.Eintraege).Where(e =>
        {
            try { return DssExportSchema.Normalisiere(ziel.Klasse, ziel.Attribut, Normwert(ziel.Klasse, ziel.Attribut, e.Label)) == wert; }
            catch (InvalidOperationException) { return false; }
        }).ToArray();
        return kandidaten.Length == 1 ? kandidaten[0].Label : wert;
    }
}
