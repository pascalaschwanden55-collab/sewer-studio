namespace AuswertungPro.Next.Domain.Geometry;

/// <summary>
/// Schweizer Landeskoordinate LV95 (EPSG:2056).
/// Ost (C1) liegt typisch zwischen 2'480'000 und 2'840'000.
/// Nord (C2) liegt typisch zwischen 1'070'000 und 1'300'000.
/// </summary>
public readonly record struct Lv95Coordinate(double Ost, double Nord)
{
    /// <summary>EPSG-Code des Referenzsystems (LV95).</summary>
    public const int Epsg = 2056;

    /// <summary>
    /// Prueft ob die Werte im plausiblen LV95-Wertebereich liegen.
    /// Verhindert dass z.B. LV03- oder WGS84-Werte versehentlich
    /// als LV95 gespeichert werden.
    /// </summary>
    public bool IsPlausibleLv95
        => Ost is > 2_000_000 and < 3_000_000
        && Nord is > 1_000_000 and < 1_400_000;
}
