using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3): Welche Spaltenansicht zeigt einzeilige Zeilen?
///
/// Anlass ist Pascals Bild vom 07.09.: In "Alle Spalten" brachte ein mehrzeiliger Text
/// ("Primaere Schaeden", "Empfohlene Massnahmen") die ganze Zeile auf drei bis vier Zeilen
/// Hoehe, und es waren nur noch sechs Haltungen sichtbar. Kompakt, Stammdaten, Sanierung und
/// Kosten fuehren keine solche Spalte und bekommen deshalb eine feste Zeilenhoehe; nur
/// "Alle Spalten" und "Bewertung" bleiben auf Auto, damit dort nichts abgeschnitten wird.
/// Reine Regel ohne WPF; die Seite wendet sie nur an.
/// </summary>
public static class DataPageZeilenhoehePolicy
{
    /// <summary>
    /// Die Ansichten ohne lange Textspalte. Bewusst eine Positivliste: Eine unbekannte oder
    /// fehlende Ansicht bleibt auf Auto und schneidet damit nichts ab.
    /// </summary>
    private static readonly HashSet<string> Einzeilig =
        new(["kompakt", "stammdaten", "sanierung", "kosten"], StringComparer.OrdinalIgnoreCase);

    public static bool IstEinzeilig(string? ansichtsSchluessel)
        => ansichtsSchluessel is not null && Einzeilig.Contains(ansichtsSchluessel);

    /// <summary>
    /// Nova-Fixwelle 2b (P2): Welche Mindesthoehe gilt fuer die Zeilen?
    ///
    /// Die Tabelle traegt neben der Zeilenhoehe eine frei einstellbare Mindesthoehe
    /// (Ansicht -> Zeilenhoehe, Werkseinstellung 38). Sie ist groesser als die kompakte
    /// Zeilenhoehe und hat das Token <c>RowHeightCompact</c> bisher vollstaendig ausgehebelt:
    /// Gemessen blieb die Zeile bei 38 px, egal welcher Wert im Token stand. In einer
    /// einzeiligen Ansicht gilt deshalb die kleinere der beiden Zahlen — eine bewusst KLEINER
    /// eingestellte Mindesthoehe bleibt erhalten. Sonst gilt allein die Einstellung.
    /// </summary>
    /// <param name="einzeilig">Gilt die kompakte Zeilenhoehe fuer diese Ansicht?</param>
    /// <param name="kompakt">Der Wert des Tokens <c>RowHeightCompact</c>.</param>
    /// <param name="eingestellt">Die vom Benutzer eingestellte Mindesthoehe.</param>
    public static double Mindesthoehe(bool einzeilig, double kompakt, double eingestellt)
        => einzeilig && kompakt > 0 ? Math.Min(kompakt, eingestellt) : eingestellt;
}
