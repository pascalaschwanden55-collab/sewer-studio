using System.Linq;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public sealed class GridSpatialIndexTests
{
    [Fact]
    public void Query_liefert_nur_schneidende_items()
    {
        var index = new GridSpatialIndex<string>(cellSize: 10);
        index.Add(new MapBounds(0, 0, 5, 5), "A");
        index.Add(new MapBounds(100, 100, 105, 105), "B");

        var hits = index.Query(new MapBounds(1, 1, 2, 2));

        Assert.Contains("A", hits);
        Assert.DoesNotContain("B", hits);
    }

    [Fact]
    public void Item_ueber_mehrere_zellen_wird_nur_einmal_geliefert()
    {
        var index = new GridSpatialIndex<string>(cellSize: 10);
        index.Add(new MapBounds(0, 0, 35, 35), "gross"); // ueberspannt mehrere 10er-Zellen

        var hits = index.Query(new MapBounds(5, 5, 30, 30));

        Assert.Equal(1, hits.Count(h => h == "gross"));
    }

    [Fact]
    public void Abfrage_ausserhalb_ist_leer()
    {
        var index = new GridSpatialIndex<string>(cellSize: 10);
        index.Add(new MapBounds(0, 0, 5, 5), "A");

        Assert.Empty(index.Query(new MapBounds(1000, 1000, 1010, 1010)));
    }

    [Fact]
    public void Zaehlt_hinzugefuegte_items()
    {
        var index = new GridSpatialIndex<int>(cellSize: 50);
        index.Add(new MapBounds(0, 0, 1, 1), 1);
        index.Add(new MapBounds(200, 200, 201, 201), 2);

        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void Grosse_streuung_findet_nur_den_ziel_cluster()
    {
        var index = new GridSpatialIndex<int>(cellSize: 100);
        for (var i = 0; i < 1000; i++)
            index.Add(new MapBounds(i * 1000, 0, i * 1000 + 10, 10), i);

        var hits = index.Query(new MapBounds(499_000, 0, 499_010, 10));

        Assert.Equal(new[] { 499 }, hits);
    }
}
