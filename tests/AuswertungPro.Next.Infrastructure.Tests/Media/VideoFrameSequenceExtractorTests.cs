using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Tests.Media;

/// <summary>
/// Holt eine ganze Bildfolge in einem ffmpeg-Durchgang. Der ffmpeg-Aufruf selbst
/// ist als Naht injizierbar, damit die Regeln ohne installiertes ffmpeg pruefbar
/// bleiben.
/// </summary>
public sealed class VideoFrameSequenceExtractorTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-frames-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Die_erzeugten_Bilder_werden_mit_ihrer_Videozeit_geliefert()
    {
        var ziel = Path.Combine(_root, "lauf");
        var extractor = Erzeuge((_, _) => SchreibeBilder(ziel, 3));

        var bilder = await extractor.ExtractAsync(Auftrag(ziel), CancellationToken.None);

        Assert.Equal(3, bilder.Count);
        Assert.Equal(new[] { 1, 2, 3 }, bilder.Select(b => b.Index));
        Assert.Equal(new[] { 0.0, 1.0, 2.0 }, bilder.Select(b => b.TimeSeconds));
        Assert.All(bilder, bild => Assert.True(File.Exists(bild.FilePath)));
    }

    [Fact]
    public async Task Ein_nicht_leerer_Zielordner_wird_abgewiesen()
    {
        // Bilder eines frueheren Laufs duerfen nie stillschweigend mitgezaehlt werden.
        var ziel = Path.Combine(_root, "lauf");
        Directory.CreateDirectory(ziel);
        File.WriteAllText(Path.Combine(ziel, "f000001.jpg"), "alt");
        var extractor = Erzeuge((_, _) => { });

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(Auftrag(ziel), CancellationToken.None));

        Assert.Contains("leer", fehler.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ein_fehlgeschlagener_ffmpeg_Aufruf_wird_gemeldet()
    {
        var ziel = Path.Combine(_root, "lauf");
        var extractor = Erzeuge((_, _) => { }, exitCode: 1, standardError: "moov atom not found");

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(Auftrag(ziel), CancellationToken.None));

        Assert.Contains("moov atom not found", fehler.Message);
    }

    [Fact]
    public async Task Ein_Lauf_ohne_ein_einziges_Bild_ist_ein_Fehler()
    {
        // Ein defektes Video liefert stumm null Bilder; das darf nicht als
        // "keine Befunde" durchgehen.
        var ziel = Path.Combine(_root, "lauf");
        var extractor = Erzeuge((_, _) => Directory.CreateDirectory(ziel));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(Auftrag(ziel), CancellationToken.None));

        Assert.Contains("kein Bild", fehler.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fremde_Dateien_im_Zielordner_werden_uebergangen()
    {
        var ziel = Path.Combine(_root, "lauf");
        var extractor = Erzeuge((_, _) =>
        {
            SchreibeBilder(ziel, 2);
            File.WriteAllText(Path.Combine(ziel, "ffmpeg.log"), "x");
            File.WriteAllText(Path.Combine(ziel, "vorschau.jpg"), "x");
        });

        var bilder = await extractor.ExtractAsync(Auftrag(ziel), CancellationToken.None);

        Assert.Equal(2, bilder.Count);
    }

    [Fact]
    public async Task Die_Abtastrate_erscheint_in_den_ffmpeg_Argumenten()
    {
        var ziel = Path.Combine(_root, "lauf");
        string? gesehen = null;
        var extractor = Erzeuge((_, argumente) =>
        {
            gesehen = argumente;
            SchreibeBilder(ziel, 1);
        });

        await extractor.ExtractAsync(
            Auftrag(ziel) with { FramesPerSecond = 2.0 }, CancellationToken.None);

        Assert.Contains("fps=2", gesehen);
    }

    [Fact]
    public async Task Ein_fehlendes_Video_wird_vor_dem_Start_abgewiesen()
    {
        var extractor = Erzeuge((_, _) => Assert.Fail("ffmpeg darf gar nicht starten."));

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => extractor.ExtractAsync(
                Auftrag(Path.Combine(_root, "lauf")) with { VideoPath = Path.Combine(_root, "fehlt.mpg") },
                CancellationToken.None));
    }

    private VideoFrameSequenceRequest Auftrag(string ziel)
    {
        var video = Path.Combine(_root, "video.mpg");
        Directory.CreateDirectory(_root);
        if (!File.Exists(video))
            File.WriteAllText(video, "video");
        return new VideoFrameSequenceRequest
        {
            FfmpegPath = Path.Combine(_root, "ffmpeg.exe"),
            VideoPath = video,
            TargetDirectory = ziel
        };
    }

    private VideoFrameSequenceExtractor Erzeuge(
        Action<string, string> beimStart,
        int exitCode = 0,
        string standardError = "")
    {
        Directory.CreateDirectory(_root);
        var ffmpeg = Path.Combine(_root, "ffmpeg.exe");
        if (!File.Exists(ffmpeg))
            File.WriteAllText(ffmpeg, "x");

        return new VideoFrameSequenceExtractor((pfad, argumente, _) =>
        {
            beimStart(pfad, argumente);
            return Task.FromResult(new ProcessRunResult(exitCode, standardError));
        });
    }

    private static void SchreibeBilder(string ziel, int anzahl)
    {
        Directory.CreateDirectory(ziel);
        for (var index = 1; index <= anzahl; index++)
            File.WriteAllText(Path.Combine(ziel, $"f{index:D6}.jpg"), "bild");
    }
}
