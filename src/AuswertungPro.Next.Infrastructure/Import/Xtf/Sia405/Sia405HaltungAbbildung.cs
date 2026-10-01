using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Sia405;

/// <summary>
/// Schritt 3 des SIA405-Haltungsimports: bildet eine Haltung mit aufgeloesten Bezuegen
/// auf einen <see cref="HaltungRecord"/> ab. Hier stehen die fachlichen Regeln — welche
/// Datei-Angabe in welches Programmfeld geht und was als leer gilt. Alle Werte tragen
/// <see cref="FieldSource.Xtf405"/>; den Schutz von Handwerten uebernimmt danach das
/// Zusammenfuehren mit dem Projekt.
/// </summary>
internal static class Sia405HaltungAbbildung
{
    public static HaltungRecord BaueRecord(Sia405HaltungMitBezuegen h)
    {
        ArgumentNullException.ThrowIfNull(h);

        var rec = new HaltungRecord();
        SetzeHaltungsfelder(rec, h);
        if (h.Kanal is { } kanal)
            SetzeKanalfelder(rec, h, kanal);

        // Schacht-Labels (optional, für Debug/Logging)
        if (!string.IsNullOrWhiteSpace(h.SchachtOben)) Setze(rec, "Schacht_oben", h.SchachtOben);
        if (!string.IsNullOrWhiteSpace(h.SchachtUnten)) Setze(rec, "Schacht_unten", h.SchachtUnten);

        return rec;
    }

    private static void SetzeHaltungsfelder(HaltungRecord rec, Sia405HaltungMitBezuegen h)
    {
        var hd = h.Haltung;
        Setze(rec, "Haltungsname", h.Haltungsname);
        Setze(rec, FieldKeys.CadastreObjectId, hd.Tid);

        var material = XtfValueNormalizer.NormalizeSiaMaterial(hd.Material);
        if (!string.IsNullOrWhiteSpace(hd.Laenge)) Setze(rec, "Haltungslaenge_m", hd.Laenge);
        if (!string.IsNullOrWhiteSpace(material)) Setze(rec, "Rohrmaterial", material);

        var dn = !string.IsNullOrWhiteSpace(hd.LichteHoehe) ? hd.LichteHoehe : hd.LichteBreite;
        if (!string.IsNullOrWhiteSpace(dn)) Setze(rec, "DN_mm", dn);

        // Die zweite Dimension: Profiltyp und Hoehen-Breiten-Verhaeltnis stehen am
        // verwiesenen Rohrprofil. Aus Hoehe und Verhaeltnis entsteht die Breite;
        // beim Kreisprofil ist sie gleich der Hoehe (rund = beide gleich).
        if (h.Rohrprofil is { } profil)
        {
            var profiltyp = (profil.Profiltyp ?? "").Trim();
            if (profiltyp.Length > 0)
                Setze(rec, FieldKeys.ProfileType, ProfiltypVokabular.Normalisieren(profiltyp));

            var breite = XtfRohrprofilVerhaeltnis.Breite(dn, profil.Verhaeltnis)
                         ?? (string.Equals(profiltyp, "Kreisprofil", StringComparison.OrdinalIgnoreCase)
                             ? SiaAbmessung.NachMillimeter(dn)
                             : null);
            if (breite is > 0)
                Setze(rec, FieldKeys.ClearWidthMm, breite.Value.ToString(CultureInfo.InvariantCulture));
        }
        else if (!string.IsNullOrWhiteSpace(hd.LichteHoehe) && !string.IsNullOrWhiteSpace(hd.LichteBreite))
        {
            // Aeltere Modellfassungen fuehren die Breite direkt an der Haltung. Auch ein
            // Profilverweis ohne Ziel faellt heute hierher (RohrprofilVerweisOhneZiel).
            Setze(rec, FieldKeys.ClearWidthMm, hd.LichteBreite.Trim());
        }

        // Keine Inspektionsrichtung: SIA405 ist der Kataster-Bestand und kennt keine Untersuchung.
        // Sie kommt aus der VSA-KEK-Untersuchung (<Fliessrichtung>), siehe ParseVsaKek.

        // Letzte_Aenderung ist das Aenderungsdatum des Datensatzes im Kataster, kein
        // Inspektionsdatum. Bis 2026-09-03 landete es in "Datum_Jahr" und ueberschrieb
        // dort das echte Inspektionsdatum aus WinCan: Aus 06.10.2025 wurde 03.09.2026.
        // Es gehoert in das Herkunftsfeld, das auch das QGIS-Nachfuellen verwendet.
        // Seit 01.10.2026 auch im ISO-Format (2025-10-06, mit oder ohne Uhrzeit).
        var letzteAenderung = XtfValueNormalizer.NormalizeDate(hd.LetzteAenderung);
        if (!string.IsNullOrWhiteSpace(letzteAenderung))
            Setze(rec, FieldKeys.CadastreLastChange, letzteAenderung);

        if (!string.IsNullOrWhiteSpace(hd.Lagebestimmung) && !string.Equals(hd.Lagebestimmung.Trim(), "unbekannt", StringComparison.OrdinalIgnoreCase))
            Setze(rec, FieldKeys.PositionAccuracy, hd.Lagebestimmung.Trim());
    }

    private static void SetzeKanalfelder(HaltungRecord rec, Sia405HaltungMitBezuegen h, Sia405KanalObjekt kanal)
    {
        var nutzungsart = XtfValueNormalizer.NormalizeNutzungsart(kanal.Nutzungsart);
        if (!string.IsNullOrWhiteSpace(kanal.Standortname)) Setze(rec, "Strasse", kanal.Standortname);
        if (!string.IsNullOrWhiteSpace(nutzungsart)) Setze(rec, "Nutzungsart", nutzungsart);
        if (!string.IsNullOrWhiteSpace(kanal.Bemerkung)) Setze(rec, "Bemerkungen", kanal.Bemerkung);

        // Ein nicht leeres Textelement hat Vorrang vor dem Organisationsverweis; ein leeres
        // verdeckt ihn nicht. Der Rohwert der XTF ("Abwasser Uri") faerbt die
        // Eigentuemerspalte der Excel-Vorlage nicht - sie vergleicht exakt gegen "AWU".
        var eigentuemer = EigentumVokabular.Normalisieren(
            string.IsNullOrWhiteSpace(kanal.Eigentuemer) ? h.EigentuemerAusVerweis : kanal.Eigentuemer);
        if (!string.IsNullOrWhiteSpace(eigentuemer)) Setze(rec, "Eigentuemer", eigentuemer);

        // "unbekannt" ist hier ein echter Organisationsname und bleibt erhalten.
        Uebernimm(rec, FieldKeys.DataOwner, h.Datenherr, unbekanntIstLeer: false);
        Uebernimm(rec, FieldKeys.DataSupplier, h.Datenlieferant, unbekanntIstLeer: false);

        // FunktionHierarchisch -> Katalog-Combo "PAA.<Suffix>" / "SAA.<Suffix>" (speist u.a. VSA-Zustandsnote B4)
        var funktion = NormalizeFunktionHierarchisch(kanal.Funktion);
        if (!string.IsNullOrWhiteSpace(funktion)) Setze(rec, "FunktionHierarchisch", funktion);

        // Baujahr ist das Baujahr, kein Inspektionsdatum. Bis 2026-09-03 fuellte es
        // ersatzweise "Datum_Jahr"; jetzt geht es in das Feld, das der Export liest.
        Uebernimm(rec, FieldKeys.ConstructionYear, kanal.Baujahr);

        // Die uebrigen Kanalfelder, die der Export selbst hinausschreibt. Ohne sie
        // verlor die Rundreise Export, Import, Vergleich genau diese Werte.
        Uebernimm(rec, FieldKeys.OperatingStatus, kanal.Status);
        Uebernimm(rec, FieldKeys.RehabilitationNeed, kanal.Sanierungsbedarf);
        Uebernimm(rec, FieldKeys.HydraulicFunction, kanal.FunktionHydraulisch);
        Uebernimm(rec, FieldKeys.ConnectionType, kanal.Verbindungsart);
        Uebernimm(rec, FieldKeys.BeddingEncasement, kanal.BettungUmhuellung);
        Uebernimm(rec, FieldKeys.GrossCost, kanal.Bruttokosten);

        // Status -> offen/abgeschlossen (wie PS)
        var status = kanal.Status ?? "";
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Regex.IsMatch(status, "(?i)in_Betrieb|aktiv"))
                Setze(rec, "Offen_abgeschlossen", "abgeschlossen");
            else if (Regex.IsMatch(status, "(?i)ausser_Betrieb|stillgelegt"))
                Setze(rec, "Offen_abgeschlossen", "offen");
        }

        // Zustandsklasse aus der Datei uebernehmen. Sie gewinnt beim Import
        // bewusst gegen jede eigene Rechnung: Nur so sind die Daten in GEONIS
        // und in SewerStudio nach einem Austausch identisch.
        var zustand = ZustandsklasseAusXtf(kanal.BaulicherZustand);
        if (zustand is not null)
            Setze(rec, "Zustandsklasse", zustand);

        // Zugaenglichkeit als Bemerkung ergänzen
        if (!string.IsNullOrWhiteSpace(kanal.Zugaenglichkeit) && !string.Equals(kanal.Zugaenglichkeit, "unbekannt", StringComparison.OrdinalIgnoreCase))
        {
            var existing = rec.GetFieldValue("Bemerkungen") ?? "";
            var add = $"Zugaenglichkeit: {kanal.Zugaenglichkeit}";
            Setze(rec, "Bemerkungen", string.IsNullOrWhiteSpace(existing) ? add : (existing + "\n" + add));
        }
    }

    private static void Setze(HaltungRecord rec, string feld, string? wert)
        => rec.SetFieldValue(feld, wert, FieldSource.Xtf405, userEdited: false);

    /// <summary>
    /// Ein Normwert aus der Datei, so wie er dort steht. "unbekannt" ist keine
    /// Angabe und wuerde nur einen besseren Wert aus einer anderen Quelle blockieren.
    /// </summary>
    private static void Uebernimm(HaltungRecord rec, string feld, string? wert, bool unbekanntIstLeer = true)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0
            || (unbekanntIstLeer
                && string.Equals(text, "unbekannt", StringComparison.OrdinalIgnoreCase)))
            return;
        Setze(rec, feld, text);
    }

    /// <summary>
    /// "Z0" bis "Z4" aus der XTF zur Ziffer, die das Programm fuehrt. Alles andere —
    /// "unbekannt", leer, ein unerwarteter Text — liefert <c>null</c> und setzt nichts.
    /// </summary>
    internal static string? ZustandsklasseAusXtf(string? roh)
    {
        var wert = (roh ?? "").Trim();
        if (wert.Length != 2 || (wert[0] != 'Z' && wert[0] != 'z'))
            return null;

        return wert[1] is >= '0' and <= '4' ? wert[1].ToString() : null;
    }

    // Bekannte FunktionHierarchisch-Suffixe (ohne "PAA."-Praefix), passend zu FieldCatalog.ComboItems.
    private static readonly string[] FunktionHierarchischSuffixe =
    {
        "Sammelkanal", "Hauptsammelkanal", "Hauptsammelkanal_regional",
        "Liegenschaftsentwaesserung", "Sanierungsleitung",
        "Strassenentwaesserung", "Gewaesser"
    };

    /// <summary>
    /// Normalisiert die SIA405-Funktion (Funktionhierarchisch) auf einen GUELTIGEN Katalog-Combo-Wert
    /// "PAA.&lt;Suffix&gt;". Verarbeitet gaengige Rohformen (mit/ohne "PAA."-Praefix, Sub-Level-Trenner "."
    /// wie "Hauptsammelkanal.regional", Umlaute). Liefert leer, wenn der Rohwert keinem bekannten Suffix
    /// entspricht — dann wird das Feld NICHT gesetzt (kein ungueltiger Combo-Wert im Datagrid).
    /// </summary>
    internal static string NormalizeFunktionHierarchisch(string? raw)
    {
        var v = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            return "";

        // Ein Normwert wie "SAA.Liegenschaftsentwaesserung" geht unveraendert durch.
        // Frueher wurde jeder Wert auf "PAA." umgeschrieben; die sekundaere Anlage (SAA)
        // fiel dabei heraus, obwohl der Kataster sie fuehrt und der Export sie schreibt.
        var norm = SiaKanalVokabular.FunktionHierarchisch.NachNorm(v);
        if (norm is not null && !norm.EndsWith(".unbekannt", StringComparison.OrdinalIgnoreCase))
            return norm;

        if (v.StartsWith("PAA.", StringComparison.OrdinalIgnoreCase))
            v = v.Substring(4);

        // Sub-Level-Trenner "." -> "_" (Hauptsammelkanal.regional -> Hauptsammelkanal_regional)
        v = v.Replace('.', '_');
        // Umlaute -> ASCII (Katalog nutzt ...entwaesserung)
        v = v.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
             .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue");

        foreach (var known in FunktionHierarchischSuffixe)
            if (string.Equals(v, known, StringComparison.OrdinalIgnoreCase))
                return "PAA." + known;

        return "";
    }
}
