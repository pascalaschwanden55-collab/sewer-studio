using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Holen der Felder, die in SewerStudio nur in der Objektakte stehen (24.09.2026, Wunsch Pascal «importiere das
/// was im WebGIS ist» und «auch bei den Schächten alle fehlenden Felder aus dem WebGIS ergänzen»): Typ AA
/// (PAA/SAA) an Haltung und Schacht, am Schacht dazu die ganze Maske, soweit die Schachtakte ein Gegenstueck
/// hat. Die Listen der Akte tragen die WebGIS-Schluessel als Originalcode — uebernommen wird ueber den
/// Schluessel. «Unbekannt» fuellt nichts; ein Handwert (auch bewusst leer) bleibt; ein Wert ohne Handmarke
/// stammt aus GeoShop und wird ersetzt (WebGIS vor GeoShop, wie die Materialgruppe). Reine Regel.
///
/// Die refIds stammen aus der Masken-Inventur v2 (nur Rotation, Gelaende-/Sohlenhoehe, Ebene, Lagebestimmung
/// und Funktion hierarchisch sind live geprueft). Jede Auswahlliste zaehlt deshalb nur, wenn die gelesene
/// Liste zur Liste der Akte passt; ohne bekannte refId wird die EINE passende Liste der Maske gesucht.
/// </summary>
public static class WebGisImportAktenfelder
{
    public const string SchachtTypAaFeld = "schacht.typ_aa";
    public const string SchachtFunktionHierarchischFeld = "schacht.funktion_hierarchisch";
    public const string SchachtFunktionHydraulischFeld = "schacht.funktion_hydraulisch";

    private const string Grund = "Feld der Objektakte aus dem WebGIS.";

    private enum Art { Liste, Organisation, Zahl, Jahr, Text }

    private sealed record Eintrag(string FeldId, string Anzeige, string? RefId, Art Art);

    /// <summary>Schachtmaske -> Schachtakte. RefId null: die Liste ist auf der Maske eindeutig und wird gesucht.</summary>
    private static readonly Eintrag[] Schacht =
    [
        new(SchachtFunktionHierarchischFeld, "Funktion hierarchisch", "2bb1a598-71a2-7cc4-b659-61301a68de69", Art.Liste),
        new(SchachtFunktionHydraulischFeld, "Funktion hydraulisch", null, Art.Liste),
        new("schacht.lagebestimmung", "Lagebestimmung", "2ff6f922-036f-29d3-62e6-04a5d091b2f7", Art.Liste),
        new("schacht.lagegenauigkeit", "Lagegenauigkeit", null, Art.Liste),
        new("schacht.hoehenbestimmung", "Höhenbestimmung", "9af92fed-ee6b-cda2-046d-ca6b1b593e20", Art.Liste),
        new("schacht.hoehengenauigkeit", "Höhengenauigkeit", null, Art.Liste),
        new("schacht.ebene", "Ebene", "32523f9a-37eb-773c-cd1a-9ca74e968566", Art.Liste),
        new("schacht.zugaenglichkeit", "Zugänglichkeit", "67e362fb-387e-f80f-19d4-8f2a006b1f9b", Art.Liste),
        new("schacht.intervention", "Interventionsmöglichkeit", "0ddf06cc-d5b3-5bfb-2792-b221e7316bfd", Art.Liste),
        new("schacht.amphibienausstieg", "Amphibienausstieg", "c125a461-2d4b-9c29-f461-6b63e86380c4", Art.Liste),
        new("schacht.informationsquelle", "Informationsquelle", "baafb76f-e816-aa48-dd3a-95325e01b0bb", Art.Liste),
        new("schacht.steuerung", "Steuerung/Fernwirkung", "349c049c-3d76-7bfe-7842-e56e7e1c7b8d", Art.Liste),
        new("schacht.finanzierung", "Finanzierung", "f24beb43-ecca-d7c0-92e3-d57cfedf7b92", Art.Liste),
        new("schacht.wiederbeschaffungswert_bauart", "Wiederbeschaffungswert Bauart", "9d7a4d4d-32b4-2af9-6fdc-ddca714e13cd", Art.Liste),
        new("schacht.systemgrenze", "Systemgrenze", "45ccde36-d90f-5ad9-fe87-4da9211652e9", Art.Liste),
        new("schacht.buero", "Büro", "64fda99b-3191-9fcb-8db2-e1d4a700653f", Art.Organisation),
        new("schacht.standortgemeinde", "Standortgemeinde", "5709cf95-e1cc-f279-176e-03e619201e07", Art.Organisation),
        new("schacht.rotation", "Rotation", "ec0e9ade-9e28-1f1b-a9a3-7e5ac9ff94af", Art.Zahl),
        new("schacht.gelaendehoehe", "Geländehöhe", "ef6b54ec-fa18-22a6-679e-3febd4c01c5e", Art.Zahl),
        new("schacht.sohlenhoehe", "Sohlenhöhe", "e627d56a-9401-1ca3-acee-736e061d77ed", Art.Zahl),
        new("schacht.deckelhoehe", "Deckelhöhe", "ad2a4ddd-2ac7-2b29-fc18-1dc3afc0e88e", Art.Zahl),
        new("schacht.rueckstaukote", "Rückstaukote", "dccbb19c-d75a-f530-d49e-06af8c37648a", Art.Zahl),
        new("schacht.hfrei", "Hfrei [m]", "ff265d40-8c4b-14cd-b993-f982471840d8", Art.Zahl),
        new("schacht.wiederbeschaffungswert", "Wiederbeschaffungswert", "94e92a97-66fd-6377-722a-71009c65558d", Art.Zahl),
        new("schacht.inspektionsintervall", "Inspektionsintervall [Jahr]", "845731db-1747-85dc-3dba-87520d64f962", Art.Zahl),
        new("schacht.spuelintervall", "Spülintervall [Jahr]", "b18e7ba0-eb35-3ee8-176b-4f4a1a380477", Art.Zahl),
        new("schacht.wiederbeschaffungswert_basisjahr", "Wiederbeschaffungswert Basisjahr", "b48ef440-5eac-2755-8a27-ea5acbe26392", Art.Jahr),
        new("schacht.erhebungsjahr_zustand", "Erhebungsjahr [Zustand]", "52f1902f-6759-e421-0873-a897111dbebb", Art.Jahr),
        new("schacht.sachbearbeiter", "Sachbearbeiter", "d7e6bee7-1c03-e0f9-23ba-542c8209dc9c", Art.Text),
        new("schacht.akten", "Akten", "44234a10-5c5b-ceae-61a2-f4ebb3257d83", Art.Text),
    ];

    /// <summary>
    /// Kartenfelder des Schachts, die in SewerStudio nur in der Akte sichtbar sind. Frueher schrieb das Holen sie
    /// in Tabellenfelder, die die Schachttabelle gar nicht fuehrt (nur «Bisherige Angaben» der Akte); die
    /// Hoehenrechnung liest aber die Akte. Das Holen fuehrt sie deshalb nur noch ueber die Akte.
    /// </summary>
    public static bool SchachtNurUeberAkte(string sewerStudioFeld) => sewerStudioFeld is
        FieldKeys.HierarchicalFunction or "Sohlenhoehe" or "Gelaendehoehe" or "Rotation" or "Ebene"
        or FieldKeys.PositionAccuracy;

    /// <summary>Alle Aktenfelder, die das Holen an dieser Objektart schreibt.</summary>
    public static IReadOnlyList<string> Felder(WebGisObjektart art) => art == WebGisObjektart.Haltung
        ? [WebGisImportPlanBuilder.TypAaFeld]
        : [SchachtTypAaFeld, .. Schacht.Select(x => x.FeldId)];

    public static bool IstAktenfeld(WebGisObjektart art, string feld) => Felder(art).Contains(feld, StringComparer.Ordinal);

    /// <summary>Lesbarer Name eines Aktenfelds (Bericht, Holen-Fenster); null, wenn es keines ist.</summary>
    public static string? Anzeige(string feld) => feld switch
    {
        WebGisImportPlanBuilder.TypAaFeld or SchachtTypAaFeld => "Typ AA",
        _ => Schacht.FirstOrDefault(x => x.FeldId == feld)?.Anzeige,
    };

    /// <summary>
    /// PAA oder SAA aus dem WebGIS-Feld «Typ AA»; null bei leer, «Unbekannt» oder unsicherem Feld. Die refId stammt
    /// aus der Inventur: Fuehrt die Liste der Komponente nicht PAA UND SAA, ist es nicht Typ AA (Hinweis).
    /// </summary>
    public static string? WebGisTypAa(WebGisImportEingabe e, WebGisLesestand stand, WebGisImportPosition pos)
    {
        var refId = WebGisFeldkarte.TypAaRef(e.Objektart);
        var key = (stand.Feld(refId) ?? string.Empty).Trim();
        if (key.Length == 0) return null;
        if (!stand.Kataloge.TryGetValue(refId, out var liste)
            || !liste.Exists(k => k.Text?.Trim() == "PAA") || !liste.Exists(k => k.Text?.Trim() == "SAA"))
        {
            pos.Hinweise.Add("Typ AA: Feld im WebGIS nicht sicher erkannt (keine PAA/SAA-Liste) — nicht übernommen.");
            return null;
        }
        var text = liste.FirstOrDefault(k => k.Key == key).Text?.Trim();
        return text is "PAA" or "SAA" ? text : null;
    }

    public static void Plane(WebGisImportEingabe e, WebGisLesestand stand, WebGisImportPosition pos, string? webgisTypAa)
    {
        var typAaFeld = e.Objektart == WebGisObjektart.Haltung ? WebGisImportPlanBuilder.TypAaFeld : SchachtTypAaFeld;
        if (webgisTypAa is not null)
        {
            // Typ AA nie gegen die Funktion der Haltung setzen: Bleibt dort «PAA.…» der Kanalfirma stehen, waere
            // ein SAA in der Akte ein Widerspruch im selben Datensatz. Am Schacht fuehrt nur die Akte die Funktion
            // (ohne PAA/SAA); ein altes verstecktes Tabellenfeld zaehlt dort nicht.
            var funktion = e.Objektart == WebGisObjektart.Haltung ? Funktion(e, pos) : string.Empty;
            var praefix = funktion.IndexOf('.') is > 0 and var i ? funktion[..i] : null;
            if (praefix is not null && !string.Equals(praefix, webgisTypAa, StringComparison.OrdinalIgnoreCase))
                pos.Hinweise.Add($"Typ AA: WebGIS «{webgisTypAa}», SewerStudio-Funktion «{funktion}» — nicht übernommen.");
            else
                Uebernimm(e, pos, typAaFeld, "Typ AA", Schluessel(stand, WebGisFeldkarte.TypAaRef(e.Objektart)), webgisTypAa);
        }

        if (e.Objektart != WebGisObjektart.Schacht) return;
        foreach (var x in Schacht)
        {
            if (x.Art is Art.Liste or Art.Organisation)
            {
                var refId = x.RefId ?? GesuchteListe(stand, x.FeldId);
                if (refId is null || Schluessel(stand, refId) is not { } key) continue;
                if (!ListePasst(stand, refId, x))
                {
                    pos.Hinweise.Add($"{x.Anzeige}: Feld im WebGIS nicht sicher erkannt (Liste passt nicht zur Objektakte) — nicht übernommen.");
                    continue;
                }
                Uebernimm(e, pos, x.FeldId, x.Anzeige, key, Klartext(stand, refId));
                continue;
            }
            var roh = (stand.Feld(x.RefId!) ?? string.Empty).Trim();
            if (roh.Length == 0) continue;
            var wert = x.Art switch
            {
                Art.Zahl => Zahl(roh),
                Art.Jahr => Jahr(roh),
                _ => roh,
            };
            if (wert is null)
            {
                pos.Hinweise.Add($"{x.Anzeige}: «{roh}» im WebGIS ist kein gültiger Wert — nicht übernommen.");
                continue;
            }
            UebernimmText(e, pos, x, wert);
        }
    }

    /// <summary>Die Funktion hierarchisch nach diesem Holen: der geplante neue Wert, sonst der vorhandene.</summary>
    private static string Funktion(WebGisImportEingabe e, WebGisImportPosition pos)
    {
        var geplant = pos.Aenderungen.FirstOrDefault(a => a.Feld == FieldKeys.HierarchicalFunction)?.Neu;
        if (!string.IsNullOrWhiteSpace(geplant)) return geplant.Trim();
        return e.Felder.TryGetValue(FieldKeys.HierarchicalFunction, out var f) ? f.Wert.Trim() : string.Empty;
    }

    /// <summary>Eintraege der Aktenliste ohne «Unbekannt» und ohne Leerwahl: (Schluessel, gefalteter Text).</summary>
    private static List<(string Key, string Text)> AktenListe(string feldId)
    {
        var katalog = FieldCatalog.Objektfelder.Auswahl(FieldCatalog.Objektfelder.Feld(feldId).KatalogId);
        return (katalog?.Eintraege ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.OriginalCode) && !string.IsNullOrWhiteSpace(x.Label)
                        && WebGisHandwertKarte.Falte(x.Label) != "unbekannt")
            .Select(x => (x.OriginalCode!.Trim(), WebGisHandwertKarte.Falte(x.Label)))
            .ToList();
    }

    /// <summary>
    /// Passt die gelesene WebGIS-Liste zur Aktenliste? Auswahlliste: mindestens zwei Eintraege mit gleichem Schluessel
    /// UND gleichem Text, kein gleicher Schluessel mit anderem Text. Organisationsliste (Texte anders geschrieben):
    /// der Organisationsschluessel «Bund» steht darin.
    /// </summary>
    private static bool ListePasst(WebGisLesestand stand, string refId, Eintrag x)
    {
        if (!stand.Kataloge.TryGetValue(refId, out var liste)) return false;
        if (x.Art == Art.Organisation)
            return liste.Exists(k => string.Equals(k.Key, WebGisFeldkarte.OrganisationBundKey, StringComparison.OrdinalIgnoreCase));
        return Passt(liste, AktenListe(x.FeldId));
    }

    private static bool Passt(List<(string Key, string Text)> webgis, List<(string Key, string Text)> akte)
    {
        var gleich = 0;
        foreach (var (key, text) in webgis)
        {
            var treffer = akte.FindAll(a => a.Key == key?.Trim());
            if (treffer.Count == 0) continue;
            if (treffer.Exists(a => a.Text == WebGisHandwertKarte.Falte(text))) gleich++;
            else if (WebGisHandwertKarte.Falte(text) != "unbekannt") return false;
        }
        return gleich >= 2;
    }

    /// <summary>Ohne refId: die EINE Komponente der Maske, deren Liste zur Aktenliste passt.</summary>
    private static string? GesuchteListe(WebGisLesestand stand, string feldId)
    {
        var akte = AktenListe(feldId);
        var treffer = stand.Kataloge.Where(k => Passt(k.Value, akte)).Select(k => k.Key).Take(2).ToList();
        return treffer.Count == 1 ? treffer[0] : null;
    }

    private static void Uebernimm(WebGisImportEingabe e, WebGisImportPosition pos, string feldId, string anzeige, string? key, string? text)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(text)
            || WebGisHandwertKarte.Falte(text) == "unbekannt") return;
        var eintrag = WebGisImportPlanBuilder.AkteEintrag(feldId, key, text);
        if (eintrag is null)
        {
            pos.Hinweise.Add($"{anzeige} «{text}» steht nicht in der Liste der Objektakte — nicht übernommen.");
            return;
        }
        Plane(e, pos, feldId, anzeige, eintrag.Label, gleich: alt => WebGisHandwertKarte.Falte(alt) == WebGisHandwertKarte.Falte(eintrag.Label));
    }

    private static void UebernimmText(WebGisImportEingabe e, WebGisImportPosition pos, Eintrag x, string wert)
        => Plane(e, pos, x.FeldId, x.Anzeige, wert, gleich: alt => x.Art is Art.Zahl or Art.Jahr
            ? Zahl(alt) == wert
            : string.Equals(alt.Trim(), wert, StringComparison.Ordinal));

    private static void Plane(WebGisImportEingabe e, WebGisImportPosition pos, string feldId, string anzeige, string neu, Func<string, bool> gleich)
    {
        e.Felder.TryGetValue(feldId, out var feld);
        var alt = (feld?.Wert ?? string.Empty).Trim();
        if (feld?.Handwert == true)
        {
            if (alt.Length == 0)
                pos.Hinweise.Add($"{anzeige}: in SewerStudio bewusst leer (Handeingabe) — WebGIS-Wert «{neu}» nicht übernommen.");
            return;
        }
        if (alt.Length > 0 && (!(feld?.Ersetzbar ?? false) || gleich(alt))) return;
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = feldId, Alt = alt.Length > 0 ? alt : null, Neu = neu, Grund = Grund });
    }

    private static string? Zahl(string text)
        => double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
           && !double.IsNaN(d) && !double.IsInfinity(d)
            ? d.ToString("0.###", CultureInfo.InvariantCulture)
            : null;

    private static string? Jahr(string text)
    {
        var t = text.Trim();
        return t.Length == 4 && int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var j) && j is >= 1800 and <= 2200
            ? t : null;
    }

    private static string? Schluessel(WebGisLesestand stand, string refId)
        => stand.Feld(refId) is { } k && k.Trim().Length > 0 ? k.Trim() : null;

    private static string? Klartext(WebGisLesestand stand, string refId)
    {
        var key = Schluessel(stand, refId);
        if (key is null || !stand.Kataloge.TryGetValue(refId, out var liste)) return null;
        var text = liste.FirstOrDefault(x => x.Key == key).Text;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
