using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Eingabe fuer den Importplan: was SewerStudio gerade fuehrt.</summary>
public sealed class WebGisImportEingabe
{
    public required WebGisObjektart Objektart { get; init; }
    public required string Bezeichnung { get; init; }
    public Guid RecordId { get; init; }
    public string? GespeicherteGlobalId { get; init; }
    /// <summary>Nur Haltung: aktuelle Haltungslaenge in SewerStudio (Text, Punkt als Dezimalzeichen).</summary>
    public string? Laenge { get; init; }
    public string? Baujahr { get; init; }
    /// <summary>Baujahr ist eine Handeingabe (auch bewusst leer): dann wird es nie aus dem WebGIS gefuellt.</summary>
    public bool BaujahrHandwert { get; init; }
    /// <summary>
    /// Nur Schacht: ist das Bauwerk ein Normschacht? Nur dort gilt die WebGIS-Funktionsliste
    /// (Schritt A, 23.09.2026); ein Spezialbauwerk bekommt keine Funktion aus dem WebGIS.
    /// </summary>
    public bool Normschacht { get; init; } = true;
    /// <summary>
    /// Aktueller SewerStudio-Stand der Kartenfelder (SewerStudio-Feldname -> Wert). Fehlt ein Feld,
    /// gilt es als leer.
    /// </summary>
    public Dictionary<string, WebGisImportFeld> Felder { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Ein Feld in SewerStudio. <paramref name="Ersetzbar"/>: Der Wert stammt aus GeoShop oder QGIS
/// und ist nicht von Hand gesetzt — das WebGIS darf ihn ersetzen (Entscheid Pascal 23.09.2026:
/// WebGIS vor GeoShop). Handwerte und Werte der Kanalfirmen nie.
/// </summary>
/// <paramref name="Handwert"/>: von Hand gesetzt, auch bewusst leer — beim Holen nie gefuellt oder ersetzt
/// (Entscheid Pascal 23.09.2026, gleich wie beim GeoShop-Abgleich).
public sealed record WebGisImportFeld(string Wert, bool Ersetzbar, bool Handwert = false);

/// <summary>Eine geplante Uebernahme WebGIS -> SewerStudio.</summary>
public sealed class WebGisImportAenderung
{
    public required string Feld { get; init; }
    public string? Alt { get; init; }
    public required string Neu { get; init; }
    public required string Grund { get; init; }
}

public sealed class WebGisImportPosition
{
    public required WebGisObjektart Objektart { get; init; }
    public required string Bezeichnung { get; init; }
    public Guid RecordId { get; init; }
    public string? GlobalId { get; init; }
    /// <summary>Gespeicherte GlobalID beim Planen: bestimmt, wie vor dem Uebernehmen erneut gelesen wird.</summary>
    public string? GespeicherteGlobalId { get; init; }
    /// <summary>
    /// Alle Maskenfelder beim Planen (samt «Geändert am»). Weicht der Stand vor dem Uebernehmen ab, wird
    /// das Objekt nicht uebernommen (Entscheid Pascal 23.09.2026 abends).
    /// </summary>
    public IReadOnlyDictionary<string, string?>? GelesenerStand { get; init; }
    public List<WebGisImportAenderung> Aenderungen { get; } = new();
    public List<string> Sperren { get; } = new();
    public List<string> Hinweise { get; } = new();
    public bool Uebernehmbar => Sperren.Count == 0 && Aenderungen.Count > 0;
    public bool Uebernommen { get; set; }
}

public sealed class WebGisImportPlan
{
    public List<WebGisImportPosition> Positionen { get; } = new();
    public List<string> Hinweise { get; } = new();
    /// <summary>Sanierungsmassnahmen aus dem WebGIS, die in SewerStudio noch keine Akte haben.</summary>
    public List<WebGisSanierungImport> Sanierungen { get; } = new();
    public int Uebernehmbare => Positionen.FindAll(p => p.Uebernehmbar).Count;
    public int SanierungenUebernehmbar => Sanierungen.FindAll(s => s.Uebernehmbar).Count;
    public int Gesperrte => Positionen.FindAll(p => p.Sperren.Count > 0).Count;
}

/// <summary>
/// Gegenrichtung: WebGIS -> SewerStudio. Reine Regel, kein Netz, kein Schreiben.
///
/// Regeln (Entscheid Pascal 21.09., zuletzt 23.09.2026 abends):
/// - Daten der Kanalfirmen sind der Ist-Zustand und werden nie ueberschrieben; das WebGIS ergaenzt.
/// - Haltungslaenge, Eigentuemer und Betreiber: rein informativ, nur in leere Felder, nie zurueck ins WebGIS.
/// - Baujahr nur, wenn in SewerStudio leer.
/// - Zustand, Bemerkung, Sanierung werden NICHT importiert: dort ist SewerStudio die Quelle.
/// - Die Felder der <see cref="WebGisHandwertKarte"/> (refIds live geprueft) fuellen leere Felder
///   und ersetzen Katasterwerte (WebGIS vor GeoShop, 23.09.2026); Handwerte bleiben stehen.
///   Der Klartext geht ueber <see cref="WebGisImportWert"/> auf den SewerStudio-Begriff.
/// </summary>
public static class WebGisImportPlanBuilder
{
    public const string FeldLaenge = "Haltungslaenge_m";
    public const string FeldBaujahr = "Baujahr";
    public const string FeldWebGisGlobalId = "WebGIS_GlobalID";

    public static WebGisImportPosition Baue(WebGisImportEingabe e, WebGisLesestand? stand)
    {
        ArgumentNullException.ThrowIfNull(e);
        var pos = new WebGisImportPosition
        {
            Objektart = e.Objektart, Bezeichnung = e.Bezeichnung, RecordId = e.RecordId, GlobalId = stand?.GlobalId,
            GespeicherteGlobalId = e.GespeicherteGlobalId,
            GelesenerStand = stand is null ? null : new Dictionary<string, string?>(stand.Felder, StringComparer.Ordinal),
        };
        if (stand is null)
        {
            pos.Sperren.Add(WebGisObjektLesen.NichtGefunden(e.GespeicherteGlobalId));
            return pos;
        }
        if (WebGisObjektLesen.NamensAbweichung(e.Bezeichnung, stand) is { } namensSperre)
        {
            pos.Sperren.Add(namensSperre);
            return pos;
        }

        if (!string.IsNullOrWhiteSpace(e.GespeicherteGlobalId)
            && !string.Equals(e.GespeicherteGlobalId, stand.GlobalId, StringComparison.OrdinalIgnoreCase))
        {
            pos.Sperren.Add("Gespeicherte WebGIS-GlobalID weicht vom Suchtreffer ab — Zuordnung prüfen.");
            return pos;
        }
        if (string.IsNullOrWhiteSpace(e.GespeicherteGlobalId))
            pos.Aenderungen.Add(new WebGisImportAenderung
            {
                Feld = FeldWebGisGlobalId, Neu = stand.GlobalId,
                Grund = "Eindeutiger WebGIS-Treffer mit exakt gleichem Namen.",
            });

        // 1) Haltungslaenge, Eigentuemer, Betreiber: rein informativ, nur in LEERE Felder (Entscheid Pascal
        //    23.09.2026 abends). Nie ersetzt — auch keinen GeoShop-Wert — und nie ins WebGIS zurueck.
        Informativ(e, stand, pos);

        // 2) Baujahr nur wenn in SewerStudio leer.
        // Bewusst leer (Handeingabe) = geschuetzt (Entscheid Pascal 23.09.2026).
        if (string.IsNullOrWhiteSpace(e.Baujahr) && !e.BaujahrHandwert)
        {
            var jahr = (stand.Feld(WebGisFeldkarte.BaujahrRef(e.Objektart)) ?? string.Empty).Trim();
            if (jahr.Length == 4 && int.TryParse(jahr, out _))
                pos.Aenderungen.Add(new WebGisImportAenderung
                {
                    Feld = FeldBaujahr, Alt = e.Baujahr, Neu = jahr, Grund = "In SewerStudio leer, im WebGIS vorhanden.",
                });
        }

        // 3) Felder der Karte (WebGIS vor GeoShop, Handwerte bleiben).
        Kartenfelder(e, stand, pos);

        // 4) Materialgruppe der Objektakte (steht in keinem Tabellenfeld).
        Materialgruppe(e, stand, pos);

        return pos;
    }

    private const string BreiteRef = "902695a4-5f44-e910-b2da-471c17085822";
    private const string HoeheRef = "d06f8d1f-8a09-1b22-4380-088a7ee42507";

    private const string GrundInformativ = "Rein informativ: in SewerStudio leer, im WebGIS vorhanden — geht nie ins WebGIS zurück.";

    /// <summary>
    /// Haltungslaenge (geometrisch, auf zwei Stellen), Eigentuemer und Betreiber (Entscheid Pascal 23.09.2026
    /// abends): Sie werden geholt, aber rein informativ, der Vollstaendigkeit halber — nur in ein leeres Feld,
    /// nie ueber einen vorhandenen Wert (Kanalfirma, GeoShop oder Hand), und nie zurueckgeschrieben.
    /// </summary>
    private static void Informativ(WebGisImportEingabe e, WebGisLesestand stand, WebGisImportPosition pos)
    {
        if (e.Objektart == WebGisObjektart.Haltung)
            FuelleInformativ(e, pos, FeldLaenge, "Haltungslänge", LaengeNormiert(stand.Feld(WebGisFeldkarte.HaltungLaengeGeomRef)));

        FuelleInformativ(e, pos, FieldKeys.Owner, "Eigentümer",
            Organisation(stand, WebGisFeldkarte.EigentuemerRef(e.Objektart), "Eigentümer", pos)?.Text);

        if (Organisation(stand, WebGisFeldkarte.BetreiberRef(e.Objektart), "Betreiber", pos) is { } betreiber)
        {
            var feldId = BetreiberFeld(e.Objektart);
            var eintrag = AkteEintrag(feldId, betreiber.Key, betreiber.Text);
            if (eintrag is null)
                pos.Hinweise.Add($"Betreiber «{betreiber.Text}» steht nicht in der Liste der Objektakte — nicht übernommen.");
            else
                FuelleInformativ(e, pos, feldId, "Betreiber", eintrag.Label);
        }
    }

    private static void FuelleInformativ(WebGisImportEingabe e, WebGisImportPosition pos, string feld, string anzeige, string? neu)
    {
        if (string.IsNullOrWhiteSpace(neu)) return;
        e.Felder.TryGetValue(feld, out var vorhanden);
        if ((vorhanden?.Wert ?? string.Empty).Trim().Length > 0) return; // vorhanden: nie ersetzen
        if (vorhanden?.Handwert == true)
        {
            pos.Hinweise.Add($"{anzeige}: in SewerStudio bewusst leer (Handeingabe) — WebGIS-Wert «{neu}» nicht übernommen.");
            return;
        }
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = feld, Alt = null, Neu = neu.Trim(), Grund = GrundInformativ });
    }

    /// <summary>
    /// Organisation (Schluessel + Klartext) eines Eigentuemer-/Betreiberfelds. Die refIds stammen aus der
    /// Inventur, nicht aus einer Live-Pruefung: Nur wenn die Liste der Komponente wirklich Organisationen
    /// fuehrt (Schluessel «Bund»), ist es das richtige Feld — sonst nichts, mit Hinweis. «unbekannt» fuellt nichts.
    /// </summary>
    private static (string Key, string Text)? Organisation(WebGisLesestand stand, string refId, string anzeige, WebGisImportPosition pos)
    {
        var key = (stand.Feld(refId) ?? string.Empty).Trim();
        if (key.Length == 0) return null;
        if (!stand.Kataloge.TryGetValue(refId, out var liste)
            || !liste.Exists(k => string.Equals(k.Key, WebGisFeldkarte.OrganisationBundKey, StringComparison.OrdinalIgnoreCase)))
        {
            pos.Hinweise.Add($"{anzeige}: Feld im WebGIS nicht sicher erkannt (keine Organisationsliste) — nicht übernommen.");
            return null;
        }
        var text = liste.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase)).Text;
        if (string.IsNullOrWhiteSpace(text) || WebGisHandwertKarte.Falte(text).StartsWith("unbekannt", StringComparison.Ordinal))
            return null;
        return (key, text.Trim());
    }

    /// <summary>Eintrag der Aktenliste: zuerst ueber den WebGIS-Schluessel (Originalcode), sonst ueber den Klartext.</summary>
    private static ObjektAuswahl? AkteEintrag(string feldId, string key, string text)
    {
        var eintraege = FieldCatalog.Objektfelder.Auswahl(FieldCatalog.Objektfelder.Feld(feldId).KatalogId)?.Eintraege ?? [];
        var nachKey = eintraege.Where(x => string.Equals(x.OriginalCode, key, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        if (nachKey.Count == 1) return nachKey[0];
        return GruppenEintrag(feldId, text);
    }

    private static void Kartenfelder(WebGisImportEingabe e, WebGisLesestand stand, WebGisImportPosition pos)
    {
        foreach (var karte in WebGisHandwertKarte.Felder)
        {
            if (karte.Objektart != e.Objektart) continue;
            // Breite/Hoehe der Haltung sind noch nicht geklaert (CLAUDE.md OFFEN): nur DN bei runder Haltung.
            if (karte.SewerStudioFeld is "Lichte_Breite_mm" or "Lichte_Hoehe_mm") continue;

            var webgis = WebGisText(karte, stand, pos);
            if (webgis is null) continue;

            // Die WebGIS-Funktionsliste gilt nur fuer den Normschacht (Schritt A, 23.09.2026); sonst
            // pendelte der Wert zwischen Holen und Speichern (Pumpenschacht <-> Pumpwerk).
            if (e.Objektart == WebGisObjektart.Schacht && karte.SewerStudioFeld == WebGisBegriffe.SchachtFunktion && !e.Normschacht)
            {
                pos.Hinweise.Add($"Funktion: «{webgis}» nicht übernommen — kein Normschacht (Funktionsliste dieser Bauwerksart nicht erhoben).");
                continue;
            }

            var neu = WebGisImportWert.Zuordne(e.Objektart, karte.SewerStudioFeld, webgis, out var hinweis, TypAa(e));
            if (hinweis is not null) pos.Hinweise.Add(hinweis);
            if (neu is null) continue;

            e.Felder.TryGetValue(karte.SewerStudioFeld, out var feld);
            var alt = (feld?.Wert ?? string.Empty).Trim();
            // Handwert, auch bewusst leer: nie fuellen, nie ersetzen (Entscheid Pascal 23.09.2026).
            if (feld?.Handwert == true)
            {
                if (alt.Length == 0)
                    pos.Hinweise.Add($"{karte.Anzeige}: in SewerStudio bewusst leer (Handeingabe) — WebGIS-Wert «{neu}» nicht übernommen.");
                continue;
            }
            if (alt.Length > 0 && !(feld?.Ersetzbar ?? false)) continue;
            if (alt.Length > 0 && (WebGisHandwertKarte.Falte(alt) == WebGisHandwertKarte.Falte(neu)
                                   || WebGisExportPlanBuilder.GleicherWert(alt, neu))) continue;

            pos.Aenderungen.Add(new WebGisImportAenderung
            {
                Feld = karte.SewerStudioFeld, Alt = alt.Length > 0 ? alt : null, Neu = neu,
                Grund = alt.Length > 0 ? "WebGIS geht vor dem Katasterwert (GeoShop/QGIS)." : "In SewerStudio leer, im WebGIS vorhanden.",
            });
        }
    }

    /// <summary>Feld der Objektakte «Typ AA» (PAA/SAA) der Haltung.</summary>
    public const string TypAaFeld = "haltung.aatype";

    /// <summary>PAA/SAA aus Typ AA, sonst aus dem Praefix der vorhandenen Funktion hierarchisch.</summary>
    private static string? TypAa(WebGisImportEingabe e)
    {
        if (e.Felder.TryGetValue(TypAaFeld, out var typ) && typ.Wert.Trim() is { Length: > 0 } t) return t;
        if (e.Felder.TryGetValue(FieldKeys.HierarchicalFunction, out var fh) && fh.Wert.IndexOf('.') is > 0 and var i)
            return fh.Wert[..i];
        return null;
    }

    /// <summary>Feld der Objektakte fuer den Betreiber (kein Tabellenfeld; Liste der Organisationen).</summary>
    public static string BetreiberFeld(WebGisObjektart art)
        => art == WebGisObjektart.Haltung ? "haltung.operator" : "schacht.betreiber";

    /// <summary>Feld der Objektakte fuer die Materialgruppe je Objektart (Unbekannt/Beton/Stahl/Kunststoff/Guss/Andere).</summary>
    public static string MaterialgruppeFeld(WebGisObjektart art)
        => art == WebGisObjektart.Haltung ? "haltung.pipegroup" : "schacht.materialgruppe";

    /// <summary>
    /// Die WebGIS-Gruppe (Hauptfeld des Materialpaars) in die Akte — leer oder nicht von Hand gesetzt.
    /// Nur ein Eintrag der Gruppenliste der Akte; «Unbekannt» fuellt nichts.
    /// </summary>
    private static void Materialgruppe(WebGisImportEingabe e, WebGisLesestand stand, WebGisImportPosition pos)
    {
        var karte = WebGisHandwertKarte.Felder.FirstOrDefault(k => k.Objektart == e.Objektart && k.HauptRefId is not null);
        if (karte?.HauptRefId is null) return;
        var gruppe = Klartext(stand, karte.HauptRefId);
        if (gruppe is null || WebGisHandwertKarte.Falte(gruppe) == "unbekannt") return;

        var feldId = MaterialgruppeFeld(e.Objektart);
        var eintrag = GruppenEintrag(feldId, gruppe);
        if (eintrag is null)
        {
            pos.Hinweise.Add($"Materialgruppe «{gruppe}» steht nicht in der Liste der Objektakte — nicht übernommen.");
            return;
        }
        e.Felder.TryGetValue(feldId, out var feld);
        var alt = (feld?.Wert ?? string.Empty).Trim();
        if (alt.Length > 0 && (!(feld?.Ersetzbar ?? false)
                               || WebGisHandwertKarte.Falte(alt) == WebGisHandwertKarte.Falte(eintrag.Label))) return;
        pos.Aenderungen.Add(new WebGisImportAenderung
        {
            Feld = feldId, Alt = alt.Length > 0 ? alt : null, Neu = eintrag.Label,
            Grund = "Materialgruppe der Objektakte aus dem WebGIS.",
        });
    }

    /// <summary>Eintrag der Gruppenliste der Akte zum Klartext (gefaltet); null ohne eindeutigen Treffer.</summary>
    public static ObjektAuswahl? GruppenEintrag(string feldId, string text)
    {
        var feld = FieldCatalog.Objektfelder.Feld(feldId);
        var treffer = (FieldCatalog.Objektfelder.Auswahl(feld.KatalogId)?.Eintraege ?? [])
            .Where(x => WebGisHandwertKarte.Falte(x.Label) == WebGisHandwertKarte.Falte(text)).Take(2).ToList();
        return treffer.Count == 1 ? treffer[0] : null;
    }

    /// <summary>Klartext des WebGIS-Felds; Combo nur ueber den Katalog (nie der nackte Schluessel).</summary>
    private static string? WebGisText(WebGisHandwertFeld karte, WebGisLesestand stand, WebGisImportPosition pos)
    {
        if (karte.SewerStudioFeld == "DN_mm")
        {
            var breite = Klartext(stand, BreiteRef);
            var hoehe = Klartext(stand, HoeheRef);
            if (breite is null) return null;
            if (hoehe is not null && !WebGisExportPlanBuilder.GleicherWert(breite, hoehe))
            {
                pos.Hinweise.Add($"DN: Breite {breite} und Höhe {hoehe} im WebGIS verschieden — DN nicht übernommen.");
                return null;
            }
            return breite;
        }
        if (karte.Typ == WebGisHandwertTyp.Text)
        {
            var roh = stand.Feld(karte.RefId);
            return string.IsNullOrWhiteSpace(roh) ? null : roh.Trim();
        }
        return Klartext(stand, karte.RefId) ?? (karte.HauptRefId is null ? null : Klartext(stand, karte.HauptRefId));
    }

    private static string? Klartext(WebGisLesestand stand, string refId)
    {
        var key = stand.Feld(refId);
        if (string.IsNullOrWhiteSpace(key) || !stand.Kataloge.TryGetValue(refId, out var liste)) return null;
        foreach (var (k, t) in liste)
            if (k == key) return string.IsNullOrWhiteSpace(t) ? null : t.Trim();
        return null;
    }

    /// <summary>Laenge auf zwei Stellen, Punkt als Dezimalzeichen ("11.44012297" -> "11.44"). Null bei leer/ungueltig.</summary>
    public static string? LaengeNormiert(string? text)
    {
        var t = (text ?? string.Empty).Trim().Replace(',', '.');
        if (t.Length == 0) return null;
        if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return null;
        return Math.Round(d, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
