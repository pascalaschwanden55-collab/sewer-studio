namespace AuswertungPro.Next.Domain.Geometry;

/// <summary>
/// Herkunft einer Geometrie. Macht in Reports/Karten unterscheidbar,
/// ob eine Koordinate aus einer offiziellen Quelle stammt oder
/// abgeleitet bzw. manuell eingegeben wurde.
/// </summary>
public enum GeometrySource
{
    /// <summary>Keine Geometrie vorhanden.</summary>
    None = 0,

    /// <summary>Aus XTF / SIA-405-Import (zuverlaessigste Quelle).</summary>
    Xtf = 1,

    /// <summary>Manuell vom Benutzer eingegeben oder korrigiert.</summary>
    Manual = 2,

    /// <summary>
    /// Abgeleitet aus einer anderen Geometrie (z.B. Schacht-Punkt
    /// aus Haltungs-Endpunkt). Niedrigere Vertrauensstufe als Xtf.
    /// </summary>
    Derived = 3,
}
