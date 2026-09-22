using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Art des WebGIS-Felds fuer einen Handwert.</summary>
public enum WebGisHandwertTyp
{
    /// <summary>Freitext/Zahl (EditBox): Text 1:1.</summary>
    Text,
    /// <summary>Auswahlliste: Klartext wird gegen den Katalog der Maske aufgeloest.</summary>
    Combo,
}

/// <summary>
/// Ein Handwert-Feld: SewerStudio-Feld -> WebGIS-refId.
/// <paramref name="RefId"/> ist bei Paarfeldern das Detail (z.B. Material-Detail
/// "Normalbeton (NB)"), <paramref name="HauptRefId"/> die Hauptkategorie ("Beton").
/// Ein Detailfeld wird nur benutzt, wenn SEIN Katalog den Klartext wirklich fuehrt —
/// so bestaetigt sich die refId bei jedem Lauf am gelesenen Stand selbst.
/// </summary>
public sealed record WebGisHandwertFeld(
    WebGisObjektart Objektart, string SewerStudioFeld, string RefId, string Anzeige, WebGisHandwertTyp Typ,
    string? HauptRefId = null);

/// <summary>
/// Zuordnung der von Hand geaenderten SewerStudio-Felder (FieldMeta.UserEdited) zu den Feldern
/// der WebGIS-Masken. Jede refId hier ist am 21.09.2026 live an den Masken geprueft: Schacht
/// 80461 und Haltung 80480-80478, jeweils refId gegen den in der Maske sichtbaren Wert
/// (Kontrollschacht, PAA/Mischabwasser, Sammelkanal/Freispiegelleitung, Beton/Beton-Fertigteil,
/// Rund, 800/800, Rotation 90, Tiefe 1.96 bzw. Normalbeton (NB), Kreisprofil (K), 300/300).
///
/// Nicht hier und mit eigener Regel: Zustand, Sanierungsbedarf, Bemerkung (zusammenfuehren),
/// Haltungslaenge (nie schreiben), Baujahr (nur fuellen wenn leer), Eigentuemer/Betreiber
/// (fuehrt das WebGIS).
/// </summary>
public static class WebGisHandwertKarte
{
    // Vor den Listen, weil deren Initialisierung ueber Falte() darauf zugreift
    // (statische Felder werden in Quelltext-Reihenfolge initialisiert).
    private static readonly Regex KuerzelAmEnde = new(@"\s*\([A-Za-z]{1,3}\)\s*$", RegexOptions.Compiled);

    public static readonly IReadOnlyList<WebGisHandwertFeld> Felder = new[]
    {
        // --- Haltung (awk_haltung) ---
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "Rohrmaterial", "e0850080-43aa-9888-1137-0d21b6f471c5", "Material", WebGisHandwertTyp.Combo, "3fc1cf51-0d42-9168-df93-6ab74b15e6b2"),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "Profiltyp", "9994d291-a8e8-09e9-02f5-a782ad23176c", "Profiltyp", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "Lichte_Breite_mm", "902695a4-5f44-e910-b2da-471c17085822", "Breite [mm]", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "DN_mm", "902695a4-5f44-e910-b2da-471c17085822", "Breite [mm]", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "Lichte_Hoehe_mm", "d06f8d1f-8a09-1b22-4380-088a7ee42507", "Höhe [mm]", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "Nutzungsart", "824e25e3-cbc5-b94d-7bc2-53b12a567abe", "Nutzungsart", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "FunktionHierarchisch", "85770f0e-245b-a1a6-c768-dfcd1c460de9", "Funktion hierarchisch", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "FunktionHydraulisch", "14998fd3-0b8a-cf97-1fba-e2a5117eb2e7", "Funktion hydraulisch", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "Status", "7ecf9743-e8df-c35d-fdce-1a188b72bef8", "Status", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "Verbindungsart", "fae2898a-63b5-e739-eaf0-20ae264020fa", "Verbindungsart", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Haltung, "Lagebestimmung", "b2427d99-2385-739f-dfd4-bb6dcda8f64b", "Lagebestimmung", WebGisHandwertTyp.Combo),
        // --- Schacht (awk_abwasserknoten) ---
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Funktion", "0aec3f59-388c-a7e7-e6f5-a0b50b81263a", "Funktion Bauwerk", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Nutzungsart", "35e5dd54-c039-2f93-dd8e-d31cc0e0ae59", "Nutzungsart", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Material", "5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728", "Material", WebGisHandwertTyp.Combo, "57efe6f1-4e76-3844-d13b-4c6dc0e1301f"),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Schachtform", "20888829-e165-1c4f-e997-0991e22e9be0", "Form", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Dimension 1 mm", "9cbffa1a-7b06-a884-b98f-7da25dc1158a", "Breite/Länge [mm]", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Dimension 2 mm", "76620dae-1911-3d61-5eeb-66ee9cacba20", "2. Mass [mm]", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Status", "576d3dff-e216-c25f-3e55-16d93027ca3d", "Status", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "FunktionHierarchisch", "2bb1a598-71a2-7cc4-b659-61301a68de69", "Funktion hierarchisch", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Tiefe", "db8b7f6e-234c-536f-7fbd-3b6d841d8ccd", "Tiefe [m]", WebGisHandwertTyp.Text),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Sohlenhoehe", "e627d56a-9401-1ca3-acee-736e061d77ed", "Sohlenhöhe", WebGisHandwertTyp.Text),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Gelaendehoehe", "ef6b54ec-fa18-22a6-679e-3febd4c01c5e", "Geländehöhe", WebGisHandwertTyp.Text),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Rotation", "ec0e9ade-9e28-1f1b-a9a3-7e5ac9ff94af", "Rotation", WebGisHandwertTyp.Text),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Ebene", "32523f9a-37eb-773c-cd1a-9ca74e968566", "Ebene", WebGisHandwertTyp.Combo),
        new WebGisHandwertFeld(WebGisObjektart.Schacht, "Lagebestimmung", "2ff6f922-036f-29d3-62e6-04a5d091b2f7", "Lagebestimmung", WebGisHandwertTyp.Combo),
    };

    /// <summary>
    /// Felder mit eigener Regel — nie ueber die Handwerte (Zustand/Sanierungsbedarf kommen aus
    /// der Akte, die Bemerkung wird zusammengefuehrt, die Laenge nie geschrieben, das Baujahr
    /// nur gefuellt wenn im WebGIS leer). Verglichen wird gefaltet.
    /// </summary>
    private static readonly HashSet<string> EigeneRegelGefaltet = new(StringComparer.Ordinal)
    {
        Falte("Zustandsklasse"), Falte("Sanierungsbedarf"), Falte("Bemerkungen"), Falte("Bemerkung"),
        Falte("Haltungslaenge_m"), Falte("Haltungslänge m"), Falte("Länge"), Falte("Laenge_m"),
        Falte("Baujahr"),
    };

    public static bool IstEigeneRegel(string? feld) => EigeneRegelGefaltet.Contains(Falte(feld));

    /// <summary>
    /// Felder, bei denen das WebGIS fuehrt (Entscheid Pascal 21.09.2026): Eigentuemer und
    /// Betreiber werden dort gepflegt und nie aus SewerStudio ueberschrieben — auch nicht als
    /// Handwert. Sie werden im Bericht genannt, damit klar ist, warum nichts geht.
    /// </summary>
    private static readonly HashSet<string> WebGisFuehrtGefaltet = new(StringComparer.Ordinal)
    {
        Falte("Eigentuemer"), Falte("Eigentümer"), Falte("Betreiber"),
    };

    public static bool WebGisFuehrt(string? feld) => WebGisFuehrtGefaltet.Contains(Falte(feld));

    /// <summary>
    /// Projektinterne Felder ohne Gegenstueck im Kataster (Dateipfade, laufende Nummern,
    /// Auswertungsergebnisse, Kennungen). Sie werden stillschweigend uebergangen, damit der
    /// Bericht nicht mit Hinweisen zugedeckt wird, die niemanden betreffen.
    /// </summary>
    private static readonly HashSet<string> NichtKatasterGefaltet = new(StringComparer.Ordinal)
    {
        Falte("NR"), Falte("NR."), Falte("Link"), Falte("PDF_Path"), Falte("PDF_All"), Falte("PDF_Eigen"),
        Falte("Primaere_Schaeden"), Falte("Pruefungsresultat"), Falte("Datum_Jahr"), Falte("Strasse"),
        Falte("GEONIS_Kennung"), Falte("Objekt_ID"), Falte("Datenherr"), Falte("Datenlieferant"),
        Falte("Letzte_Aenderung"), Falte("Aktualisierungsdatum"), Falte("Bruttokosten"), Falte("Kosten"),
        Falte("Schacht_oben"), Falte("Schacht_unten"), Falte("Inspektionsrichtung"), Falte("Schachtnummer"),
        Falte("Haltungsname"), Falte("VSA_Zustandsnote_B"), Falte("VSA_Zustandsnote_D"), Falte("VSA_Zustandsnote_S"),
        Falte("Sanieren_JaNein"), Falte("Empfohlene_Sanierungsmassnahmen"), Falte("Offen_abgeschlossen"),
    };

    public static bool NichtFuerKataster(string? feld) => NichtKatasterGefaltet.Contains(Falte(feld));

    public static WebGisHandwertFeld? Finde(WebGisObjektart art, string sewerStudioFeld)
    {
        foreach (var f in Felder)
            if (f.Objektart == art && Falte(f.SewerStudioFeld) == Falte(sewerStudioFeld))
                return f;
        return null;
    }

    /// <summary>
    /// Schluessel zum Klartext im Katalog. Verglichen wird gefaltet (Gross/Klein, Unterstrich,
    /// Umlaut-Umschreibung, Kuerzel in Klammern am Ende). Traegt SewerStudio einen
    /// zusammengesetzten Wert wie "PAA.Sammelkanal", zaehlt zusaetzlich der Teil nach dem Punkt.
    /// Kein Treffer -&gt; null; es wird nie geraten.
    /// </summary>
    public static string? Schluessel(IReadOnlyList<(string Key, string Text)>? katalog, string? text)
    {
        if (katalog is null) return null;
        var treffer = Suche(katalog, Falte(text));
        if (treffer is not null) return treffer;

        var roh = (text ?? string.Empty).Trim();
        var punkt = roh.LastIndexOf('.');
        return punkt > 0 && punkt < roh.Length - 1 ? Suche(katalog, Falte(roh[(punkt + 1)..])) : null;
    }

    private static string? Suche(IReadOnlyList<(string Key, string Text)> katalog, string gefaltet)
    {
        if (gefaltet.Length == 0) return null;
        foreach (var (key, wert) in katalog)
            if (Falte(wert) == gefaltet) return key;
        return null;
    }

    /// <summary>Hauptkategorie eines Detailtexts: "Beton, Fertigteil" -> "Beton".</summary>
    public static string Hauptteil(string? text)
    {
        var t = (text ?? string.Empty).Trim();
        var i = t.IndexOf(',');
        return i > 0 ? t[..i].Trim() : t;
    }

    public static string Falte(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        t = KuerzelAmEnde.Replace(t, "");
        t = t.Replace('_', ' ');
        t = t.Replace("ae", "ä").Replace("oe", "ö").Replace("ue", "ü");
        t = Regex.Replace(t, @"\s+", " ");
        return t.ToLowerInvariant();
    }
}
