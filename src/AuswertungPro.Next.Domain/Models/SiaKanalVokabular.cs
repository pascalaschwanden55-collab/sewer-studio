using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Eine Werteliste der SIA405-Norm, so wie sie in der Modelldatei steht.
///
/// Anders als <see cref="MaterialVokabular"/> oder <see cref="SchachtMaterialVokabular"/>
/// gibt es hier keine zweite Begriffswelt: Diese Felder kommen aus dem Kataster bereits
/// in der Schreibweise der Norm und werden im Programm auch so gefuehrt. Es gibt deshalb
/// nichts zu uebersetzen — nur zu pruefen.
///
/// <see cref="NachNorm"/> ist bewusst fail-closed: Was nicht in der Liste steht, liefert
/// <c>null</c> und wird nicht in die Datei geschrieben. Ein erfundener Wert waere
/// schlimmer als eine fehlende Angabe.
/// </summary>
public sealed class SiaWerteliste
{
    public SiaWerteliste(params string[] werte)
    {
        ArgumentNullException.ThrowIfNull(werte);

        Werte = new ReadOnlyCollection<string>(werte.ToList());
        Auswahl = new ReadOnlyCollection<string>(new[] { "" }.Concat(werte).ToList());
    }

    /// <summary>Die Werte der Norm, in der Reihenfolge der Modelldatei.</summary>
    public IReadOnlyList<string> Werte { get; }

    /// <summary>Dieselben Werte mit einem leeren Eintrag davor — fuer die Auswahl im Programm.</summary>
    public IReadOnlyList<string> Auswahl { get; }

    /// <summary>
    /// Die in SIA405 gueltige Schreibweise, oder <c>null</c>.
    ///
    /// Erkannt wird der Wert selbst ohne Ruecksicht auf Gross-/Kleinschreibung sowie eine
    /// Schreibweise mit Leerzeichen statt Unterstrich ("in Kanal aufgehaengt"). Beim
    /// zweistufigen <c>FunktionHierarchisch</c> gilt dasselbe fuer den Punkt, damit
    /// "PAA Sammelkanal" ebenfalls trifft.
    /// </summary>
    public string? NachNorm(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return null;

        var mitUnterstrich = text.Replace(' ', '_');
        var mitPunkt = text.Replace(' ', '.');

        return Werte.FirstOrDefault(w =>
            string.Equals(w, text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(w, mitUnterstrich, StringComparison.OrdinalIgnoreCase)
            || string.Equals(w, mitPunkt, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Die Wertelisten der Klassen <c>Kanal</c> und <c>Rohrprofil</c> aus SIA405.
///
/// Quelle ist die Modelldatei <c>SIA405_Abwasser_2020_1_2_d_LV95</c> vom 29.11.2025
/// (VSA-Modellablage, Modell <c>SIA405_ABWASSER_2020_1_LV95</c>). Die Schreibweise ist
/// zeichengenau verbindlich.
///
/// <c>Profiltyp</c> haengt nicht am Kanal, sondern an der eigenen Klasse
/// <c>Rohrprofil</c>, auf die die Haltung ueber <c>RohrprofilRef</c> zeigt.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class SiaKanalVokabular
{
    /// <summary><c>Rohrprofil.Profiltyp</c> — 7 Werte.</summary>
    public static readonly SiaWerteliste Profiltyp = new(
        "Eiprofil", "Kreisprofil", "Maulprofil", "offenes_Profil",
        "Rechteckprofil", "Spezialprofil", "unbekannt");

    /// <summary><c>Kanal.Verbindungsart</c> — 13 Werte.</summary>
    public static readonly SiaWerteliste Verbindungsart = new(
        "andere", "Elektroschweissmuffen", "Flachmuffen", "Flansch", "Glockenmuffen",
        "Kupplung", "Schraubmuffen", "spiegelgeschweisst", "Spitzmuffen", "Steckmuffen",
        "Ueberschiebmuffen", "unbekannt", "Vortriebsrohrkupplung");

    /// <summary><c>Kanal.Bettung_Umhuellung</c> — 14 Werte.</summary>
    public static readonly SiaWerteliste BettungUmhuellung = new(
        "andere", "erdverlegt", "in_Kanal_aufgehaengt", "in_Kanal_einbetoniert",
        "in_Leitungsgang", "in_Vortriebsrohr_Beton", "in_Vortriebsrohr_Stahl", "Sand",
        "SIA_Typ1", "SIA_Typ2", "SIA_Typ3", "SIA_Typ4", "Sohlbrett", "unbekannt");

    /// <summary>
    /// <c>Kanal.FunktionHydraulisch</c> — 12 Werte. Im Kataster zu 93,5 % gefuellt
    /// (92872-mal <c>Freispiegelleitung</c>), also ein echt gepflegtes Feld.
    /// </summary>
    public static readonly SiaWerteliste FunktionHydraulisch = new(
        "andere", "Drainagetransportleitung", "Drosselleitung", "Duekerleitung",
        "Freispiegelleitung", "Pumpendruckleitung", "Sickerleitung", "Speicherleitung",
        "Spuelleitung", "unbekannt", "Vakuumleitung", "Versickerungsleitung");

    /// <summary>
    /// <c>Abwasserbauwerk.Status</c> — 5 Werte aus dem Basismodell
    /// <c>SIA405_Base_Abwasser_1_LV95</c>. Im Kataster zu 97,8 % gefuellt.
    /// </summary>
    public static readonly SiaWerteliste Status = new(
        "ausser_Betrieb", "in_Betrieb", "tot", "unbekannt", "weitere");

    /// <summary><c>Abwasserbauwerk.Sanierungsbedarf</c> — 6 Werte.</summary>
    public static readonly SiaWerteliste Sanierungsbedarf = new(
        "dringend", "keiner", "kurzfristig", "langfristig", "mittelfristig", "unbekannt");

    /// <summary>
    /// <c>Haltung.Lagebestimmung</c> — 3 Werte. Haengt an der physischen Klasse
    /// <c>Haltung</c>, nicht am <c>Kanal</c>.
    /// </summary>
    public static readonly SiaWerteliste Lagebestimmung = new(
        "genau", "unbekannt", "ungenau");

    /// <summary>
    /// <c>Kanal.FunktionHierarchisch</c> — zweistufig, deshalb 14 Blattwerte mit Punkt.
    ///
    /// <c>PAA</c> ist die primaere, <c>SAA</c> die sekundaere Abwasseranlage. Nur die
    /// Blaetter sind gueltige Werte; die beiden Gruppennamen allein sind keine Angabe.
    /// </summary>
    public static readonly SiaWerteliste FunktionHierarchisch = new(
        "PAA.andere", "PAA.Gewaesser", "PAA.Hauptsammelkanal", "PAA.Hauptsammelkanal_regional",
        "PAA.Liegenschaftsentwaesserung", "PAA.Sammelkanal", "PAA.Sanierungsleitung",
        "PAA.Strassenentwaesserung", "PAA.unbekannt",
        "SAA.andere", "SAA.Liegenschaftsentwaesserung", "SAA.Sanierungsleitung",
        "SAA.Strassenentwaesserung", "SAA.unbekannt");
}
