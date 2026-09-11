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
        ["sanierung.grund"] = "Unterhalt.Grund", ["sanierung.kosten"] = "Unterhalt.Kosten"
    };
    public static (string Klasse, string Attribut)? Ziel(ObjektFeldDefinition f)
    {
        var text = Ergaenzungen.GetValueOrDefault(f.Id) ?? f.Exportziel;
        if (text is null) return null;
        var m = Regex.Match(text, @"^([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)(?: \(geerbt\))?$");
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : null;
    }
    public static string Normwert(string klasse, string attribut, string text)
    {
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
}
