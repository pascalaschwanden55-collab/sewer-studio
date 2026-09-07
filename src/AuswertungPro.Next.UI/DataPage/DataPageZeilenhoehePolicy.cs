using System;

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
    public static bool IstEinzeilig(string? ansichtsSchluessel)
        => !string.Equals(ansichtsSchluessel, "alle", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(ansichtsSchluessel, "bewertung", StringComparison.OrdinalIgnoreCase);
}
