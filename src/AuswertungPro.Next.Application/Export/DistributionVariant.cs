namespace AuswertungPro.Next.Application.Export;

/// <summary>
/// Verteil-Variante eines Objekts (Haltung/Schacht).
/// <see cref="Normal"/> legt die PDF direkt im Objektordner ab;
/// <see cref="Sanierung"/> schiebt eine feste Zwischen-Ebene
/// <c>{Datum}_{Objekt}_Saniert {Jahr}</c> ein und legt dieselben Dateien
/// eine Ebene tiefer ab (Video-Zuordnung bleibt erhalten).
/// </summary>
public enum DistributionVariant
{
    Normal,
    Sanierung
}
