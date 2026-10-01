using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.BendSuggestions;

/// <summary>
/// Steckt die geprueften Einzelteile zusammen: Kalibrierung, Bildextraktion,
/// gepinnter Kandidat, Zusammenfassung. Die Regeln selbst liegen in der
/// Application-Schicht; hier wird nur die Verdrahtung geprueft.
/// </summary>
public sealed class BendSuggestionScanServiceTests : IDisposable
{
    private const string Id = "bcc_nc15_seed46_20260808";
    private const string Sha = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-scan-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Ohne_Kalibrierung_wird_kein_Bild_extrahiert()
    {
        var extrahiert = false;
        var dienst = Erzeuge(
            kalibrierung: null,
            extract: _ => { extrahiert = true; return Task.FromResult(Bilder(2)); });

        var ergebnis = await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        Assert.False(ergebnis.IsUsable);
        Assert.Contains("Arbeitspunkt", ergebnis.Reason);
        Assert.False(extrahiert);
    }

    [Fact]
    public async Task Jedes_Bild_geht_mit_gepinnter_ID_und_Hash_an_den_Sidecar()
    {
        var anfragen = new List<BccTestYoloRequest>();
        var dienst = Erzeuge(
            kalibrierung: Kalibrierung(),
            extract: _ => Task.FromResult(Bilder(3)),
            antwort: anfrage =>
            {
                anfragen.Add(anfrage);
                return Antwort(0.9);
            });

        var ergebnis = await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        Assert.True(ergebnis.IsUsable);
        Assert.Equal(3, anfragen.Count);
        Assert.All(anfragen, a => Assert.Equal(Id, a.CandidateId));
        Assert.All(anfragen, a => Assert.Equal(Sha, a.CandidateSha256));
    }

    [Fact]
    public async Task Die_Vorschlaege_tragen_die_kalibrierten_Grenzen()
    {
        var dienst = Erzeuge(
            kalibrierung: Kalibrierung(),
            extract: _ => Task.FromResult(Bilder(2)),
            antwort: _ => Antwort(0.9));

        var ergebnis = await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        Assert.Equal(0.50, ergebnis.MinConfidence, 3);
        Assert.Equal(0.80, ergebnis.StrongConfidence, 3);
        Assert.Single(ergebnis.Suggestions);
        Assert.Equal(BendSuggestionStrength.Strong, ergebnis.Suggestions[0].Strength);
    }

    [Fact]
    public async Task Der_Arbeitsordner_wird_danach_wieder_entfernt()
    {
        var dienst = Erzeuge(
            kalibrierung: Kalibrierung(),
            extract: _ => Task.FromResult(Bilder(1)),
            antwort: _ => Antwort(0.9));

        await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        Assert.Empty(Directory.GetDirectories(Path.Combine(_root, "arbeit")));
    }

    [Fact]
    public async Task Der_Arbeitsordner_wird_auch_nach_einem_Fehler_entfernt()
    {
        var dienst = Erzeuge(
            kalibrierung: Kalibrierung(),
            extract: _ => throw new InvalidOperationException("ffmpeg kaputt"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dienst.ScanAsync(Auftrag(), CancellationToken.None));

        Assert.Empty(Directory.GetDirectories(Path.Combine(_root, "arbeit")));
    }

    [Fact]
    public async Task Zwei_Laeufe_stossen_sich_nicht_am_selben_Ordner()
    {
        // Der Extraktor verlangt einen leeren Zielordner; ein fester Name wuerde
        // beim zweiten Lauf scheitern oder Bilder des ersten mitzaehlen.
        var dienst = Erzeuge(
            kalibrierung: Kalibrierung(),
            extract: _ => Task.FromResult(Bilder(1)),
            antwort: _ => Antwort(0.9));

        await dienst.ScanAsync(Auftrag(), CancellationToken.None);
        var zweiter = await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        Assert.True(zweiter.IsUsable);
    }

    [Fact]
    public async Task Der_Meterstand_der_Antwort_wird_zum_Ort_des_Vorschlags()
    {
        // Ende-zu-Ende-Verdrahtung: meter_value aus der Sidecar-Antwort durch
        // Detektor, Folge und Aggregator bis in den sichtbaren Vorschlag.
        var dienst = Erzeuge(
            kalibrierung: Kalibrierung(),
            extract: _ => Task.FromResult(Bilder(2)),
            antwort: _ => Antwort(0.9, meterValue: 12.3));

        var ergebnis = await dienst.ScanAsync(Auftrag(), CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Suggestions);
        Assert.Equal(12.3, vorschlag.MeterStart);
        Assert.False(vorschlag.MeterIsEstimated);
    }

    private BendSuggestionScanRequest Auftrag() => new()
    {
        VideoPath = Path.Combine(_root, "video.mpg"),
        CandidateId = Id,
        WeightSha256 = Sha
    };

    private static BendSuggestionCalibration Kalibrierung() => new()
    {
        CandidateId = Id,
        WeightSha256 = Sha,
        MinConfidence = 0.50,
        StrongConfidence = 0.80,
        Source = "Videomessung 2026-08-08"
    };

    private IReadOnlyList<VideoSequenceFrame> Bilder(int anzahl)
    {
        var ordner = Path.Combine(_root, "bilder");
        Directory.CreateDirectory(ordner);
        var bilder = new List<VideoSequenceFrame>();
        for (var index = 1; index <= anzahl; index++)
        {
            var pfad = Path.Combine(ordner, $"f{index:D6}.jpg");
            File.WriteAllBytes(pfad, [1, 2, 3]);
            bilder.Add(new VideoSequenceFrame(index, 10 + index - 1, pfad));
        }

        return bilder;
    }

    private static BccTestYoloResponse Antwort(double konfidenz, double? meterValue = null) => new(
        Available: true, Error: null, IsRelevant: true,
        Detections: [new YoloDetectionDto(0, 0, 10, 10, "BCC_bogen", konfidenz)],
        FrameClass: "relevant", InferenceTimeMs: 10.0,
        CandidateId: Id, CandidateSha256: Sha, ModelName: "bcc", Device: "cuda:0",
        MeterValue: meterValue);

    private BendSuggestionScanService Erzeuge(
        BendSuggestionCalibration? kalibrierung,
        Func<VideoFrameSequenceRequest, Task<IReadOnlyList<VideoSequenceFrame>>> extract,
        Func<BccTestYoloRequest, BccTestYoloResponse>? antwort = null)
    {
        Directory.CreateDirectory(_root);
        var video = Path.Combine(_root, "video.mpg");
        if (!File.Exists(video))
            File.WriteAllText(video, "video");

        return new BendSuggestionScanService(
            new FesteKalibrierung(kalibrierung),
            new FesterExtraktor(extract),
            (anfrage, _) => Task.FromResult(
                antwort?.Invoke(anfrage) ?? Antwort(0.9)),
            () => Path.Combine(_root, "ffmpeg.exe"),
            () => Path.Combine(_root, "arbeit"));
    }

    private sealed class FesteKalibrierung(BendSuggestionCalibration? wert)
        : IBendSuggestionCalibrationStore
    {
        public BendSuggestionCalibration? TryRead(string candidateId) => wert;
    }

    private sealed class FesterExtraktor(
        Func<VideoFrameSequenceRequest, Task<IReadOnlyList<VideoSequenceFrame>>> extract)
        : IVideoFrameSequenceExtractor
    {
        public Task<IReadOnlyList<VideoSequenceFrame>> ExtractAsync(
            VideoFrameSequenceRequest request, CancellationToken cancellationToken)
        {
            // Wie der echte Extraktor: Er legt den Zielordner an. Ohne das haetten
            // die Aufraeumtests nichts zu pruefen.
            Directory.CreateDirectory(request.TargetDirectory);
            File.WriteAllText(Path.Combine(request.TargetDirectory, "f000001.jpg"), "bild");
            return extract(request);
        }
    }
}
