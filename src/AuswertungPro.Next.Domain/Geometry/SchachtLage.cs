namespace AuswertungPro.Next.Domain.Geometry;

/// <summary>
/// Punktgeometrie eines Schachts in LV95 (EPSG:2056).
/// </summary>
public sealed class SchachtLage
{
    public required Lv95Coordinate Punkt { get; init; }

    public required GeometrySource Source { get; init; }
}
