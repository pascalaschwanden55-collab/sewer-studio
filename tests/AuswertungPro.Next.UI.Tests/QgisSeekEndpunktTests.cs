using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Video;
using AuswertungPro.Next.UI.QgisBridge;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Rueckweg der QGIS-Bruecke: POST /qgis/seek. Der Router uebersetzt den Rumpf und
/// meldet ehrlich, was aus dem Sprung wurde — das Plugin zeigt den Grund im Status.
///
/// Die Tests dieser Klasse setzen das gemeinsame Sprungziel und nehmen es danach
/// wieder zurueck; sie laufen deshalb bewusst in einer Klasse, also nacheinander.
/// </summary>
public sealed class QgisSeekEndpunktTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"qgis-seek-{Guid.NewGuid():N}");

    public void Dispose()
    {
        QgisBridgeVideoSprung.SetzeZiel(null);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    // ---- Rumpf lesen -------------------------------------------------------

    [Fact]
    public void EinGueltigerAuftrag_WirdGelesen()
    {
        var gelesen = QgisSeekAnfrage.TryLies(
            """{"haltung": "80475-80462", "meter": 18.75}""",
            out var auftrag);

        Assert.True(gelesen);
        Assert.Equal("80475-80462", auftrag!.Haltung);
        Assert.Equal(18.75, auftrag.Meter, 3);
    }

    [Fact]
    public void EineZahlAlsZeichenkette_WirdMitPunktGelesen()
    {
        Assert.True(QgisSeekAnfrage.TryLies("""{"haltung": "A-B", "meter": "18.75"}""", out var auftrag));
        Assert.Equal(18.75, auftrag!.Meter, 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein json")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"meter": 18.75}""")]
    [InlineData("""{"haltung": "A-B"}""")]
    [InlineData("""{"haltung": "", "meter": 18.75}""")]
    [InlineData("""{"haltung": "A-B", "meter": null}""")]
    [InlineData("""{"haltung": "A-B", "meter": "achtzehn"}""")]
    public void EinUnklarerAuftrag_WirdAbgewiesen(string? body)
    {
        Assert.False(QgisSeekAnfrage.TryLies(body, out var auftrag));
        Assert.Null(auftrag);
    }

    // ---- Antworten des Endpunkts ------------------------------------------

    [Fact]
    public void EinAusgefuehrterSprung_MeldetErfolg()
    {
        QgisBridgeVideoSprung.SetzeZiel(_ => VideoSprungGrund.Bereit);

        var response = CreateRouter().RoutePost("/qgis/seek", """{"haltung": "A-B", "meter": 5.0}""");

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void DerAuftragErreichtDasZiel_Unveraendert()
    {
        VideoSprungAuftrag? empfangen = null;
        QgisBridgeVideoSprung.SetzeZiel(auftrag =>
        {
            empfangen = auftrag;
            return VideoSprungGrund.Bereit;
        });

        CreateRouter().RoutePost("/qgis/seek", """{"haltung": "80475-80462", "meter": 12.4}""");

        Assert.Equal("80475-80462", empfangen!.Haltung);
        Assert.Equal(12.4, empfangen.Meter, 3);
    }

    [Fact]
    public void OhneLaufendesVideo_Gibt404()
    {
        QgisBridgeVideoSprung.SetzeZiel(null);

        var response = CreateRouter().RoutePost("/qgis/seek", """{"haltung": "A-B", "meter": 5.0}""");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public void EineFremdeHaltung_Gibt409()
    {
        QgisBridgeVideoSprung.SetzeZiel(_ => VideoSprungGrund.FremdeHaltung);

        var response = CreateRouter().RoutePost("/qgis/seek", """{"haltung": "A-B", "meter": 5.0}""");

        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public void EinUnklarerRumpf_Gibt400OhneDasZielZuRufen()
    {
        var gerufen = false;
        QgisBridgeVideoSprung.SetzeZiel(_ =>
        {
            gerufen = true;
            return VideoSprungGrund.Bereit;
        });

        var response = CreateRouter().RoutePost("/qgis/seek", "kein json");

        Assert.Equal(400, response.StatusCode);
        Assert.False(gerufen);
    }

    [Fact]
    public void EineStoerungImPlayer_ReisstDieBrueckeNichtMit()
    {
        QgisBridgeVideoSprung.SetzeZiel(_ => throw new InvalidOperationException("Player weg"));

        var response = CreateRouter().RoutePost("/qgis/seek", """{"haltung": "A-B", "meter": 5.0}""");

        Assert.Equal(409, response.StatusCode);
    }

    // ---- Grenzen des schreibenden Wegs ------------------------------------

    [Theory]
    [InlineData("/qgis/status.json")]
    [InlineData("/qgis/current.geojson")]
    [InlineData("/qgis/video_position.json")]
    [InlineData("/qgis/irgendwas")]
    [InlineData("/")]
    public void KeinAndererPfad_LaesstSichBeschreiben(string path)
    {
        var gerufen = false;
        QgisBridgeVideoSprung.SetzeZiel(_ =>
        {
            gerufen = true;
            return VideoSprungGrund.Bereit;
        });

        var response = CreateRouter().RoutePost(path, """{"haltung": "A-B", "meter": 5.0}""");

        Assert.Equal(404, response.StatusCode);
        Assert.False(gerufen);
    }

    [Fact]
    public void EinLesenderAufrufSpringtNie()
    {
        var gerufen = false;
        QgisBridgeVideoSprung.SetzeZiel(_ =>
        {
            gerufen = true;
            return VideoSprungGrund.Bereit;
        });

        CreateRouter().Route("/qgis/seek", QgisProjectSnapshot.Empty);

        Assert.False(gerufen);
    }

    [Fact]
    public void EineAbfrageAmPfad_AenderetDasZielNicht()
    {
        QgisBridgeVideoSprung.SetzeZiel(_ => VideoSprungGrund.Bereit);

        var response = CreateRouter().RoutePost("/qgis/seek?t=1", """{"haltung": "A-B", "meter": 5.0}""");

        Assert.Equal(200, response.StatusCode);
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
