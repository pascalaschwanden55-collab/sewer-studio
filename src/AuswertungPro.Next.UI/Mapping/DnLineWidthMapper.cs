namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Nennweite (DN in mm) -> Linienbreite der Haltung auf der Karte.
/// Dickere Rohre werden dicker gezeichnet — die Netz-Hierarchie wird auf einen Blick lesbar
/// (Hausanschluss vs. Sammler). Unbekannte DN behalten die bisherige Einheitsbreite 4.
/// </summary>
public static class DnLineWidthMapper
{
    public static double Breite(int? dnMm) => dnMm switch
    {
        null or <= 0 => 4.0,   // unbekannt: heutige Einheitsbreite
        < 250 => 2.5,          // Hausanschluesse
        < 400 => 3.5,          // Standardrohre
        < 700 => 4.5,          // grosse Rohre
        _ => 6.0               // Sammler
    };
}
