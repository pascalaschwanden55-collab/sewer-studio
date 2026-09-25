using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Video;
using AuswertungPro.Next.UI.QgisBridge;

namespace AuswertungPro.Next.UI.Tests;

public sealed class QgisVideoPositionEndpunktTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "qgis-video-position-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SpielendesVideoMitLaenge_LiefertAktuellenMeterwertUndQuelle()
    {
        QgisBridgeVideoPosition.SetzeQuelle(() => new VideoPositionEingang(
            "A-B",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(100),
            Playing: true,
            50,
            Array.Empty<VideoMeterStuetzstelle>()));

        var response = CreateRouter().Route("/qgis/video_position.json", QgisProjectSnapshot.Empty);

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("A-B", json.RootElement.GetProperty("haltung").GetString());
        Assert.Equal(15, json.RootElement.GetProperty("meter").GetDouble(), 3);
        Assert.Equal("MittlereGeschwindigkeit", json.RootElement.GetProperty("meterQuelle").GetString());
    }

    [Fact]
    public void OhneBelastbarenMeterwert_Liefert404StattGeratenemMarker()
    {
        QgisBridgeVideoPosition.SetzeQuelle(() => new VideoPositionEingang(
            "A-B",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(100),
            Playing: true,
            null,
            Array.Empty<VideoMeterStuetzstelle>()));

        var response = CreateRouter().Route("/qgis/video_position.json", QgisProjectSnapshot.Empty);

        Assert.Equal(404, response.StatusCode);
    }

    public void Dispose()
    {
        QgisBridgeVideoPosition.SetzeQuelle(null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
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
}
