using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Gleichmaessiger Gitter-Index fuer Bounding-Box-Abfragen. Ein Item wird in alle Zellen
/// eingetragen, die seine Box beruehrt; <see cref="Query"/> sammelt Kandidaten aus den
/// ueberlappten Zellen und prueft die echte Ueberschneidung. Ersetzt lineares Durchsuchen
/// (O(n)) durch ~O(Treffer) — entscheidend fuer fluessiges Schwenken/Zoomen bei zehntausenden
/// Netzlinien. Bauen ist einmalig; danach ist der Index unveraenderlich zu lesen.
/// </summary>
public sealed class GridSpatialIndex<T>
{
    private readonly double _cellSize;
    private readonly List<(MapBounds Bounds, T Item)> _items = new();
    private readonly Dictionary<long, List<int>> _cells = new();

    public GridSpatialIndex(double cellSize)
    {
        if (cellSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Zellgroesse muss > 0 sein.");
        _cellSize = cellSize;
    }

    public int Count => _items.Count;

    public void Add(MapBounds bounds, T item)
    {
        var index = _items.Count;
        _items.Add((bounds, item));

        var (cx0, cy0, cx1, cy1) = CellRange(bounds);
        for (var cx = cx0; cx <= cx1; cx++)
            for (var cy = cy0; cy <= cy1; cy++)
            {
                var key = CellKey(cx, cy);
                if (!_cells.TryGetValue(key, out var list))
                    _cells[key] = list = new List<int>();
                list.Add(index);
            }
    }

    public IReadOnlyList<T> Query(MapBounds region)
    {
        var result = new List<T>();
        var seen = new HashSet<int>();

        var (cx0, cy0, cx1, cy1) = CellRange(region);
        for (var cx = cx0; cx <= cx1; cx++)
            for (var cy = cy0; cy <= cy1; cy++)
            {
                if (!_cells.TryGetValue(CellKey(cx, cy), out var list))
                    continue;

                foreach (var i in list)
                {
                    if (!seen.Add(i))
                        continue; // ueber mehrere Zellen gespanntes Item nur einmal
                    if (_items[i].Bounds.Intersects(region))
                        result.Add(_items[i].Item);
                }
            }

        return result;
    }

    private (int, int, int, int) CellRange(MapBounds b)
        => ((int)Math.Floor(b.MinX / _cellSize),
            (int)Math.Floor(b.MinY / _cellSize),
            (int)Math.Floor(b.MaxX / _cellSize),
            (int)Math.Floor(b.MaxY / _cellSize));

    // Zwei 32-bit-Zellkoordinaten kollisionsfrei in einen 64-bit-Schluessel packen.
    private static long CellKey(int cx, int cy)
        => ((long)cx << 32) ^ (uint)cy;
}
