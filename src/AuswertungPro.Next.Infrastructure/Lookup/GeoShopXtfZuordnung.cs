using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Lookup;

internal static class GeoShopXtfZuordnung
{
    public static GeoShopBauteil Baue(GeoShopXtfObjekt o, BauteilArt art,
        IReadOnlyDictionary<string, GeoShopXtfObjekt> alle, IReadOnlySet<string> mehrfach)
    {
        var fehler = new List<string>();
        void Pruefe(GeoShopXtfObjekt objekt)
        {
            if (!SiaObjektkennung.IstGueltig(objekt.Tid) || mehrfach.Contains(objekt.Tid))
                fehler.Add($"Ungültige oder doppelte TID: {objekt.Tid}");
        }
        GeoShopXtfObjekt? Hole(GeoShopXtfObjekt? von, string rolle, params string[] klassen)
        {
            var id = von?.Ref(rolle) ?? "";
            if (alle.TryGetValue(id, out var ziel) && klassen.Contains(ziel.Klasse))
            { Pruefe(ziel); return ziel; }
            fehler.Add($"Verknüpfung fehlt oder passt nicht: {rolle} ({id})");
            return null;
        }
        Pruefe(o);
        var name = o.Wert("Bezeichnung");
        var haltung = art == BauteilArt.Haltung;
        var bw = Hole(o, "AbwasserbauwerkRef", haltung ? ["Kanal"] :
            ["Normschacht", "Spezialbauwerk", "Versickerungsanlage", "Einleitstelle"]);
        var profil = haltung ? Hole(o, "RohrprofilRef", "Rohrprofil") : null;
        var von = haltung ? Hole(o, "vonHaltungspunktRef", "Haltungspunkt") : null;
        var nach = haltung ? Hole(o, "nachHaltungspunktRef", "Haltungspunkt") : null;
        var vonKnoten = haltung ? Hole(von, "AbwassernetzelementRef", "Abwasserknoten") : null;
        var nachKnoten = haltung ? Hole(nach, "AbwassernetzelementRef", "Abwasserknoten") : null;
        // Letzte_Aenderung der XTF ist NICHT GN_LAST_EDITED_DATE. Kein erfundener GEONIS-Ausgangsstand.
        var k = haltung
            ? KatasterKennung.FuerHaltung(name, null, o.Tid, bw?.Tid, von?.Tid, von?.Wert("Bezeichnung"),
                nach?.Tid, nach?.Wert("Bezeichnung"), profil?.Tid, profil?.Wert("Profiltyp"))
            : KatasterKennung.FuerSchacht(name, null, o.Tid, bw?.Tid);

        var roh = new Dictionary<string, string>(StringComparer.Ordinal);
        string? hinweis = null;
        void Feld(GeoShopXtfObjekt? objekt, string xtf, string qgis)
        {
            var wert = objekt?.Wert(xtf) ?? "";
            if (xtf == "Letzte_Aenderung" && DateTime.TryParseExact(wert, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum))
                wert = datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            roh[qgis] = wert;
        }
        foreach (var (xtf, qgis) in new[] {
            ("Status", "bw_status"), ("Sanierungsbedarf", "bw_sanierungsbedarf"),
            ("BaulicherZustand", "bw_baulicherzustand"), ("Baujahr", "bw_baujahr"),
            ("Bruttokosten", "bw_bruttokosten"), ("Bemerkung", "bw_bemerkung") })
            Feld(bw, xtf, qgis);
        if (bw is not null && alle.TryGetValue(bw.Ref("EigentuemerRef"), out var org)
            && org.Klasse == "Organisation" && !mehrfach.Contains(org.Tid))
            Feld(org, "Bezeichnung", "org_eigentuemer");
        else if (!string.IsNullOrWhiteSpace(bw?.Ref("EigentuemerRef")))
            hinweis = "Eigentümer ist nur als Verweis vorhanden; ohne Organisationsobjekt wird kein Eigentümer ergänzt.";
        Feld(o, "Letzte_Aenderung", haltung ? "ha_letzte_aenderung" : "letzte_aenderung");
        if (haltung)
        {
            foreach (var (xtf, qgis) in new[] { ("Material", "ha_material"), ("Lichte_Hoehe", "ha_lichte_hoehe"),
                ("LaengeEffektiv", "ha_laengeeffektiv"), ("Lagebestimmung", "ha_lagebestimmung"),
                ("Innenschutz", "ha_innenschutz") }) Feld(o, xtf, qgis);
            foreach (var (xtf, qgis) in new[] { ("Nutzungsart_Ist", "ka_nutzungsart_ist"),
                ("FunktionHierarchisch", "ka_funktionhierarchisch"), ("FunktionHydraulisch", "ka_funktionhydraulisch"),
                ("Verbindungsart", "ka_verbindungsart"), ("Bettung_Umhuellung", "ka_bettung_umhuellung") }) Feld(bw, xtf, qgis);
        }
        else
        {
            foreach (var (xtf, qgis) in new[] { ("Material", "ns_material"), ("Funktion", "ns_funktion"),
                ("Dimension1", "ns_dimension1"), ("Dimension2", "ns_dimension2") }) Feld(bw, xtf, qgis);
        }
        var bauteil = new QgisBauteil(name, roh);
        var felder = QgisFeldKarte.Felder(art)
            .Select(f => (Feld: f, Wert: QgisFeldKarte.Wert(bauteil, f, art)))
            .Where(p => !string.IsNullOrWhiteSpace(p.Wert))
            .ToDictionary(p => p.Feld, p => p.Wert!, StringComparer.Ordinal);
        if (haltung)
        {
            var typ = SiaKanalVokabular.Profiltyp.NachNorm(profil?.Wert("Profiltyp") ?? "");
            if (!string.IsNullOrEmpty(typ) && typ != "unbekannt") felder[FieldKeys.ProfileType] = typ;
            if (decimal.TryParse(o.Wert("Lichte_Hoehe"), NumberStyles.Float, CultureInfo.InvariantCulture, out var hoehe)
                && decimal.TryParse(profil?.Wert("HoehenBreitenverhaeltnis"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var verhaeltnis) && hoehe > 0 && verhaeltnis > 0)
                felder[FieldKeys.ClearWidthMm] = decimal.Round(hoehe / verhaeltnis, 0).ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(vonKnoten?.Wert("Bezeichnung"))) felder["Schacht_oben"] = vonKnoten.Wert("Bezeichnung");
            if (!string.IsNullOrWhiteSpace(nachKnoten?.Wert("Bezeichnung"))) felder["Schacht_unten"] = nachKnoten.Wert("Bezeichnung");
        }
        else if (bw is not null)
        {
            felder[FieldKeys.ShaftStructureType] = bw.Klasse;
            // Spezialfunktionen nicht durch das Normschacht-Vokabular verallgemeinern.
            if (bw.Klasse != "Normschacht") felder.Remove("Funktion");
            if (bw.Klasse == "Spezialbauwerk" && AbwasserbauwerkVokabular.Spezialfunktion(bw.Wert("Funktion")) is { } funktion
                && funktion != "unbekannt") felder["Funktion"] = funktion;
            if (bw.Klasse == "Versickerungsanlage" && AbwasserbauwerkVokabular.Versickerungsarten.Contains(bw.Wert("Art"))
                && bw.Wert("Art") is not ("" or "unbekannt")) felder[FieldKeys.InfiltrationType] = bw.Wert("Art");
        }
        return new GeoShopBauteil(name, k, felder, vonKnoten?.Wert("Bezeichnung"), nachKnoten?.Wert("Bezeichnung"),
            fehler.Count == 0 ? null : string.Join("; ", fehler.Distinct()), hinweis);
    }
}
