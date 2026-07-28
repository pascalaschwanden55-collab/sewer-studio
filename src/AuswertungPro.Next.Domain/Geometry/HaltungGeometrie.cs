namespace AuswertungPro.Next.Domain.Geometry;

/// <summary>
/// Liniengeometrie einer Haltung in LV95 (EPSG:2056).
/// Mindestens zwei Punkte (Start/Ende), bei polylinienfoermigen
/// Haltungen weitere Stuetzpunkte dazwischen.
/// </summary>
public sealed class HaltungGeometrie
{
    /// <summary>Polylinie als Sequenz von LV95-Punkten (mindestens 2).</summary>
    public required IReadOnlyList<Lv95Coordinate> Verlauf { get; init; }

    /// <summary>Herkunft der Geometrie.</summary>
    public required GeometrySource Source { get; init; }

    /// <summary>Erster Punkt (Anfangsschacht-Seite).</summary>
    public Lv95Coordinate Start => Verlauf[0];

    /// <summary>Letzter Punkt (Endschacht-Seite).</summary>
    public Lv95Coordinate Ende => Verlauf[Verlauf.Count - 1];
}
