using System.IO;
using System.Text.Json;
using AuswertungPro.Next.UI.QgisBridge;

namespace AuswertungPro.Next.UI.Tests;

public sealed class QgisBridgeEndpointRouterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"qgis-router-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("/")]
    [InlineData("/qgis")]
    [InlineData("/qgis/")]
    [InlineData("/qgis/status.json")]
    [InlineData("/qgis/status.json?cache=123")]
    public void StatusPfade_LiefernAktuellenJsonVertrag(string path)
    {
        var router = CreateRouter();

        var response = router.Route(path, QgisProjectSnapshot.Empty);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        using var json = JsonDocument.Parse(response.Body);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("SewerStudio", json.RootElement.GetProperty("app").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("projectHoldingCount").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("projectSchachtCount").GetInt32());
    }

    [Theory]
    [InlineData("/qgis/current.geojson")]
    [InlineData("/qgis/damages.geojson")]
    [InlineData("/qgis/network.geojson")]
    [InlineData("/qgis/sanierungstyp.geojson")]
    [InlineData("/qgis/schaechte.geojson")]
    [InlineData("/qgis/current_schacht.geojson")]
    [InlineData("/qgis/schacht_sanierungstyp.geojson")]
    public void AlleGeoJsonPfade_LiefernLeereGueltigeFeatureCollection(string path)
    {
        var router = CreateRouter();

        var response = router.Route(path, QgisProjectSnapshot.Empty);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/geo+json; charset=utf-8", response.ContentType);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("FeatureCollection", json.RootElement.GetProperty("type").GetString());
        Assert.Empty(json.RootElement.GetProperty("features").EnumerateArray());
    }

    [Fact]
    public void UnbekannterPfad_LiefertStabilen404Vertrag()
    {
        var response = CreateRouter().Route("/qgis/unbekannt", QgisProjectSnapshot.Empty);

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        using var json = JsonDocument.Parse(response.Body);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("Unbekannter QGIS-Bridge-Endpunkt.", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void UnveraenderterGeoJsonAbruf_VerwendetSerialisiertenCache()
    {
        var router = CreateRouter();

        var first = router.Route("/qgis/network.geojson", QgisProjectSnapshot.Empty);
        var second = router.Route("/qgis/network.geojson", QgisProjectSnapshot.Empty);

        Assert.Same(first, second);
        Assert.Same(first.Body, second.Body);
    }

    private QgisBridgeEndpointRouter CreateRouter()
    {
        Directory.CreateDirectory(_directory);
        var builder = new QgisBridgeSnapshotBuilder(
            new AppSettings
            {
                AbwasserkatasterXtfPath = Path.Combine(_directory, "missing.xtf"),
                KantonUriXtfDirectory = _directory
            },
            Path.Combine(_directory, "network-cache.json"),
            Path.Combine(_directory, "manhole-cache.json"));
        return new QgisBridgeEndpointRouter(builder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
