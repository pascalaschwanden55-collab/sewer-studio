using System;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Was auf der Haltungsseite sichtbar ist. Drei Ansichten schliessen sich gegenseitig aus:
/// die Aufklapp-Liste (Standard), die Tabelle und die alte Haltungsansicht.
///
/// Reine, WPF-freie Regel ohne Seiteneffekt — der Aufrufer setzt die Sichtbarkeiten und
/// speichert die Einstellung. Die abgeleiteten Eigenschaften stehen bewusst hier und nicht
/// in der Seite: Spaltenchips, Eingabefelder und Abdocken gehoeren zur Tabelle, und diese
/// Zuordnung darf es nur an einer Stelle geben.
/// </summary>
public readonly record struct HaltungenAnsichtSichtbarkeit(bool Liste, bool Tabelle, bool AlteAnsicht)
{
    /// <summary>Spaltenansichten gibt es nur in der Tabelle; die Liste hat feste Spalten.</summary>
    public bool Spaltenchips => Tabelle;

    /// <summary>Die Eingabefelder-Schublade gehoert zur Tabelle; in der Liste steht das Formular in der Zeile.</summary>
    public bool Eingabefelder => Tabelle;

    /// <summary>Die Uebersicht rechts folgt in beiden Nova-Ansichten der Auswahl.</summary>
    public bool Uebersicht => Liste || Tabelle;

    /// <summary>Abgedockt wird die Tabelle; in der Liste ist der Menuepunkt deshalb gesperrt.</summary>
    public bool AbdockenMoeglich => Tabelle;
}

/// <summary>
/// Normalisiert <c>AppSettings.HaltungenAnsicht</c> und leitet daraus mit dem Nova-Schalter
/// die drei Sichtbarkeiten ab.
/// </summary>
public static class HaltungenAnsichtRegel
{
    /// <summary>Die Aufklapp-Liste ist der Standard.</summary>
    public const string Liste = "liste";

    /// <summary>Die bisherige Tabelle mit Statusspalten, Spaltenansichten und Abdocken.</summary>
    public const string Tabelle = "tabelle";

    /// <summary>
    /// Nur "liste" und "tabelle" sind gueltig. Alles andere — leer, unbekannt, Schreibfehler
    /// aus einer von Hand bearbeiteten settings.json — faellt auf die Liste zurueck.
    /// </summary>
    public static string Normalisiere(string? wert)
        => string.Equals(wert?.Trim(), Tabelle, StringComparison.OrdinalIgnoreCase) ? Tabelle : Liste;

    /// <summary>Ist die gespeicherte Ansicht die Aufklapp-Liste?</summary>
    public static bool IstListe(string? wert) => Normalisiere(wert) == Liste;

    /// <summary>
    /// Ohne Nova-Layout (<c>AppSettings.ShowHaltungenNovaLayout=false</c>) gilt die alte
    /// Haltungsansicht — die gespeicherte Ansicht bleibt dabei erhalten und gilt wieder,
    /// sobald der Benutzer zurueckschaltet.
    /// </summary>
    public static HaltungenAnsichtSichtbarkeit Bestimme(bool novaAktiv, string? ansicht)
    {
        if (!novaAktiv)
            return new HaltungenAnsichtSichtbarkeit(Liste: false, Tabelle: false, AlteAnsicht: true);

        var liste = IstListe(ansicht);
        return new HaltungenAnsichtSichtbarkeit(liste, !liste, AlteAnsicht: false);
    }
}
