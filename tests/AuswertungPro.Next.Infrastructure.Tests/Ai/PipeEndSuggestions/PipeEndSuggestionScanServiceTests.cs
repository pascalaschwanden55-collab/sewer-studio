using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.PipeEndSuggestions;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.PipeEndSuggestions;

/// <summary>
/// Steckt die geprueften Einzelteile zusammen: Bildextraktion mit der Abtastrate
/// der Abnahme, gepinnte Lernstufen, Regel. Die Regeln selbst liegen in der
/// Application-Schicht; hier wird nur die Verdrahtung geprueft.
/// </summary>
public sealed class PipeEndSuggestionScanServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-anfang-ende-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Jedes_Bild_geht_je_Klasse_mit_gepinnter_Klasse_und_Hash_an_den_Sidecar()
    {
        var anfragen = new List<LernstufeRequest>();
        var extractor = new FakeExtractor(bilder: 3);
        var dienst = Erzeuge(extractor, anfrage =>
        {
            anfragen.Add(anfrage);
            return Antwort(anfrage, 0.2);
        });

        var ergebnis = await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        Assert.Equal(6, anfragen.Count);
        Assert.Equal(3, ergebnis.FramesAnalyzed);
        Assert.All(anfragen.Take(3), a => Assert.Equal("rohranfang", a.Klasse));
        Assert.All(anfragen.Take(3), a => Assert.Equal(PipeEndLernstufePins.Rohranfang.WeightSha256, a.GewichtSha256));
        Assert.All(anfragen.Skip(3), a => Assert.Equal("rohrende", a.Klasse));
        Assert.All(anfragen.Skip(3), a => Assert.Equal(PipeEndLernstufePins.Rohrende.WeightSha256, a.GewichtSha256));
        // Die Bildbytes stammen aus der extrahierten Datei, nicht aus einem zweiten Weg.
        Assert.Equal(Convert.ToBase64String(FakeExtractor.BildBytes(1)), anfragen[0].ImageBase64);
        Assert.Equal(PipeEndLernstufePins.All, ergebnis.Pins);
    }

    [Fact]
    public async Task Der_Extraktor_bekommt_ffmpeg_Video_ein_Bild_je_Sekunde_und_einen_eigenen_Arbeitsordner()
    {
        var extractor = new FakeExtractor(bilder: 1);
        var dienst = Erzeuge(extractor, anfrage => Antwort(anfrage, 0.1));

        await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        var anfrage = Assert.Single(extractor.Anfragen);
        Assert.Equal(@"C:\ffmpeg\bin\ffmpeg.exe", anfrage.FfmpegPath);
        Assert.Equal(@"D:\Haltungen\H_36053-36052.mpg", anfrage.VideoPath);
        // Die Freigabe wurde mit fps=1 gemessen (lernstufe_vorschlagspruefung.py).
        Assert.Equal(1.0, anfrage.FramesPerSecond);
        Assert.StartsWith(_root, anfrage.TargetDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(_root, anfrage.TargetDirectory);
    }

    [Fact]
    public async Task Der_Arbeitsordner_wird_danach_wieder_entfernt()
    {
        var extractor = new FakeExtractor(bilder: 2);
        var dienst = Erzeuge(extractor, anfrage => Antwort(anfrage, 0.1));

        await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        var arbeitsordner = Assert.Single(extractor.Anfragen).TargetDirectory;
        Assert.False(Directory.Exists(arbeitsordner));
    }

    [Fact]
    public async Task Die_staerkste_Stelle_je_Klasse_kommt_als_Vorschlag_zurueck()
    {
        var extractor = new FakeExtractor(bilder: 30);
        var dienst = Erzeuge(extractor, anfrage =>
        {
            // Rohranfang stark in den ersten Sekunden, Rohrende stark ab Sekunde 25.
            var index = int.Parse(Path.GetFileNameWithoutExtension(BildPfad(anfrage)).AsSpan(1));
            var wert = anfrage.Klasse == "rohranfang"
                ? (index <= 3 ? 0.95 : 0.02)
                : (index >= 26 ? 0.90 : 0.03);
            return Antwort(anfrage, wert);
        });

        var ergebnis = await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        Assert.Equal(2, ergebnis.Suggestions.Count);
        Assert.Equal(PipeEndKind.Rohranfang, ergebnis.Suggestions[0].Kind);
        Assert.Equal(0.0, ergebnis.Suggestions[0].TimeStartSeconds);
        Assert.Equal(PipeEndKind.Rohrende, ergebnis.Suggestions[1].Kind);
        Assert.Equal(25.0, ergebnis.Suggestions[1].TimeStartSeconds);
    }

    [Fact]
    public async Task Der_Fortschritt_wird_je_Klasse_durchgereicht()
    {
        var extractor = new FakeExtractor(bilder: 2);
        var dienst = Erzeuge(extractor, anfrage => Antwort(anfrage, 0.1));
        var meldungen = new List<PipeEndScanProgress>();

        await dienst.ScanAsync(Auftrag(), CancellationToken.None, new SofortFortschritt(meldungen));

        Assert.Equal(
            new[]
            {
                new PipeEndScanProgress(PipeEndKind.Rohranfang, 1, 2),
                new PipeEndScanProgress(PipeEndKind.Rohranfang, 2, 2),
                new PipeEndScanProgress(PipeEndKind.Rohrende, 1, 2),
                new PipeEndScanProgress(PipeEndKind.Rohrende, 2, 2)
            },
            meldungen);
    }

    private PipeEndSuggestionScanService Erzeuge(
        FakeExtractor extractor,
        Func<LernstufeRequest, LernstufeResponse> antwort)
        => new(
            extractor,
            (anfrage, _) => Task.FromResult(antwort(anfrage)),
            () => @"C:\ffmpeg\bin\ffmpeg.exe",
            () => _root);

    private static PipeEndScanRequest Auftrag()
        => new() { VideoPath = @"D:\Haltungen\H_36053-36052.mpg" };

    /// <summary>Der Fake merkt sich je Anfrage den Bildpfad ueber die Bytes (Index im Inhalt).</summary>
    private static string BildPfad(LernstufeRequest anfrage)
    {
        var bytes = Convert.FromBase64String(anfrage.ImageBase64);
        return $"f{BitConverter.ToInt32(bytes, 0):000000}.jpg";
    }

    private static LernstufeResponse Antwort(LernstufeRequest anfrage, double konfidenz)
        => new(
            Klasse: anfrage.Klasse,
            Konfidenz: konfidenz,
            GewichtSha256: anfrage.GewichtSha256,
            FreigabeSha256: new string('c', 64),
            Precision: 0.85,
            Recall: 0.98,
            Device: "cuda:0",
            InferenceTimeMs: 5.0);

    private sealed class SofortFortschritt(List<PipeEndScanProgress> ziel) : IProgress<PipeEndScanProgress>
    {
        public void Report(PipeEndScanProgress value) => ziel.Add(value);
    }

    /// <summary>Schreibt echte kleine Dateien, damit der Dienst sie wie im Betrieb liest.</summary>
    private sealed class FakeExtractor(int bilder) : IVideoFrameSequenceExtractor
    {
        public List<VideoFrameSequenceRequest> Anfragen { get; } = [];

        public static byte[] BildBytes(int index) => BitConverter.GetBytes(index);

        public Task<IReadOnlyList<VideoSequenceFrame>> ExtractAsync(
            VideoFrameSequenceRequest request,
            CancellationToken cancellationToken)
        {
            Anfragen.Add(request);
            Directory.CreateDirectory(request.TargetDirectory);
            var frames = new List<VideoSequenceFrame>();
            for (var i = 1; i <= bilder; i++)
            {
                var pfad = Path.Combine(request.TargetDirectory, $"f{i:000000}.jpg");
                File.WriteAllBytes(pfad, BildBytes(i));
                frames.Add(new VideoSequenceFrame(i, (i - 1) / request.FramesPerSecond, pfad));
            }
            return Task.FromResult<IReadOnlyList<VideoSequenceFrame>>(frames);
        }
    }
}
