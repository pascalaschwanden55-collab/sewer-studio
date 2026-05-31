using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Map;

public readonly record struct MapBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public bool Contains(MapBounds other)
        => MinX <= other.MinX
           && MaxX >= other.MaxX
           && MinY <= other.MinY
           && MaxY >= other.MaxY;

    public bool Intersects(MapBounds other)
        => MinX <= other.MaxX
           && MaxX >= other.MinX
           && MinY <= other.MaxY
           && MaxY >= other.MinY;

    public MapBounds Grow(double marginX, double marginY)
        => new(MinX - marginX, MinY - marginY, MaxX + marginX, MaxY + marginY);
}

public sealed record ProjectedHaltungGeometry(
    string Haltungsname,
    IReadOnlyList<(double X, double Y)> Points,
    MapBounds Bounds);

public static class NetworkViewportFilter
{
    public static IReadOnlyList<ProjectedHaltungGeometry> Project(IReadOnlyList<HaltungGeometry> geometries)
    {
        var result = new List<ProjectedHaltungGeometry>(geometries.Count);

        foreach (var geometry in geometries)
        {
            if (geometry.Points.Count < 2)
                continue;

            var points = geometry.Points
                .Select(p => CoordinateTransform.Lv95ToWebMercator(p.X, p.Y))
                .ToArray();

            result.Add(new ProjectedHaltungGeometry(
                geometry.Haltungsname,
                points,
                BuildBounds(points)));
        }

        return result;
    }

    public static IReadOnlyList<ProjectedHaltungGeometry> FilterByViewport(
        IEnumerable<ProjectedHaltungGeometry> geometries,
        MapBounds viewport)
        => geometries.Where(g => g.Bounds.Intersects(viewport)).ToList();

    private static MapBounds BuildBounds(IReadOnlyList<(double X, double Y)> points)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var (x, y) in points)
        {
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        return new MapBounds(minX, minY, maxX, maxY);
    }
}
