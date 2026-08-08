using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Vorabdurchlauf: Bilder holen, je Bild das gepinnte Modell fragen, die
/// Treffer zu Vorschlaegen zusammenfassen. Aussenverbindungen sind eingehaengt,
/// damit die Regeln ohne ffmpeg und ohne Sidecar pruefbar bleiben.
/// </summary>
public sealed class BendSuggestionScanUseCaseTests
{
    private const string Id = "bcc_nc15_seed46_20260808";
    private const string Sha = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114";

    [Fact]
    public async Task Ohne_kalibrierten_Arbeitspunkt_wird_kein_Bild_angefasst()
    {
        // Ein Gewicht ohne gemessenen Arbeitspunkt darf gar nicht erst laufen.
        var extrahiert = false;
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            calibration: null,
            Aktionen(
                extract: _ => { extrahiert = true; return Task.FromResult(Bilder(3)); },
                detect: (_, _) => Task.FromResult<double?>(0.9)),
            CancellationToken.None);

        Assert.False(ergebnis.IsUsable);
        Assert.Contains("Arbeitspunkt", ergebnis.Reason);
        Assert.Empty(ergebnis.Suggestions);
        Assert.False(extrahiert);
    }

    [Fact]
    public async Task Jedes_Bild_wird_genau_einmal_gefragt()
    {
        var gefragt = new List<int>();
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Kalibrierung(),
            Aktionen(
                extract: _ => Task.FromResult(Bilder(4)),
                detect: (bild, _) => { gefragt.Add(bild.Index); return Task.FromResult<double?>(null); }),
            CancellationToken.None);

        Assert.True(ergebnis.IsUsable);
        Assert.Equal(new[] { 1, 2, 3, 4 }, gefragt);
        Assert.Equal(4, ergebnis.FramesAnalyzed);
        Assert.Empty(ergebnis.Suggestions);
    }

    [Fact]
    public async Task Treffer_werden_mit_den_kalibrierten_Grenzen_zusammengefasst()
    {
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Kalibrierung(),
            Aktionen(
                extract: _ => Task.FromResult(Bilder(4)),
                // Bild 1 unter dem Arbeitspunkt, Bild 2 und 3 darueber und benachbart.
                detect: (bild, _) => Task.FromResult<double?>(bild.Index switch
                {
                    1 => 0.30,
                    2 => 0.55,
                    3 => 0.85,
                    _ => null
                })),
            CancellationToken.None);

        var einziger = Assert.Single(ergebnis.Suggestions);
        Assert.Equal(0.85, einziger.MaxConfidence, 3);
        Assert.Equal(BendSuggestionStrength.Strong, einziger.Strength);
        Assert.Equal(2, einziger.FrameCount);
    }

    [Fact]
    public async Task Ein_technischer_Fehler_gilt_nie_als_kein_Bogen()
    {
        // Derselbe Grundsatz wie beim leeren ffmpeg-Lauf: "nichts gefunden" und
        // "nichts gesehen" sind verschiedene Aussagen.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BendSuggestionScanUseCase.ExecuteAsync(
                Auftrag(),
                Kalibrierung(),
                Aktionen(
                    extract: _ => Task.FromResult(Bilder(3)),
                    detect: (bild, _) => bild.Index == 2
                        ? throw new InvalidOperationException("Sidecar nicht erreichbar")
                        : Task.FromResult<double?>(null)),
                CancellationToken.None));
    }

    [Fact]
    public async Task Der_Kandidat_und_sein_Gewicht_stehen_im_Ergebnis()
    {
        // Ohne diese Bindung laesst sich spaeter nicht sagen, welches Modell die
        // Vorschlaege erzeugt hat — und die Herkunft der Codierung waere wertlos.
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Kalibrierung(),
            Aktionen(
                extract: _ => Task.FromResult(Bilder(1)),
                detect: (_, _) => Task.FromResult<double?>(0.9)),
            CancellationToken.None);

        Assert.Equal(Id, ergebnis.CandidateId);
        Assert.Equal(Sha, ergebnis.WeightSha256);
        Assert.Equal(0.50, ergebnis.MinConfidence, 3);
        Assert.Equal(0.80, ergebnis.StrongConfidence, 3);
    }

    [Fact]
    public async Task Die_Laufzeit_wird_gemessen_und_ausgewiesen()
    {
        // Ueber HTTP je Bild ist der Durchlauf deutlich langsamer als ein direkter
        // Modellaufruf. Wer die Laufzeit nicht ausweist, merkt eine Verschlechterung nie.
        var uhr = new Queue<DateTimeOffset>(
        [
            new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 8, 12, 1, 30, TimeSpan.Zero)
        ]);

        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Kalibrierung(),
            Aktionen(
                extract: _ => Task.FromResult(Bilder(1)),
                detect: (_, _) => Task.FromResult<double?>(null)) with { Now = uhr.Dequeue },
            CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(90), ergebnis.Duration);
    }

    [Fact]
    public async Task Ein_Abbruch_wird_durchgereicht()
    {
        using var quelle = new CancellationTokenSource();
        await quelle.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => BendSuggestionScanUseCase.ExecuteAsync(
                Auftrag(),
                Kalibrierung(),
                Aktionen(
                    extract: _ => Task.FromResult(Bilder(3)),
                    detect: (_, _) => Task.FromResult<double?>(null)),
                quelle.Token));
    }

    [Fact]
    public async Task Ohne_Meterstand_werden_die_ersten_Sekunden_als_Schachteinfahrt_verworfen()
    {
        // Der Blick vom Schacht ins Rohr sieht aus wie ein Bogen. Liegt kein
        // Meterstand vor, kann nur die Anfangszeit diese Stelle erkennen — ein
        // starker Treffer bei Sekunde 1 wird deshalb bewusst nicht gemeldet.
        var frueh = new[] { new VideoSequenceFrame(1, 1.0, "f000001.jpg") };
        var spaet = new[] { new VideoSequenceFrame(1, 30.0, "f000001.jpg") };

        var ohne = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(), Kalibrierung(),
            Aktionen(_ => Task.FromResult<IReadOnlyList<VideoSequenceFrame>>(frueh),
                     (_, _) => Task.FromResult<double?>(0.95)),
            CancellationToken.None);
        var mit = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(), Kalibrierung(),
            Aktionen(_ => Task.FromResult<IReadOnlyList<VideoSequenceFrame>>(spaet),
                     (_, _) => Task.FromResult<double?>(0.95)),
            CancellationToken.None);

        Assert.Empty(ohne.Suggestions);
        Assert.Single(mit.Suggestions);
        // Das Bild wurde trotzdem ausgewertet — verworfen wurde erst der Vorschlag.
        Assert.Equal(1, ohne.FramesAnalyzed);
    }

    private static BendSuggestionScanRequest Auftrag() => new()
    {
        VideoPath = @"D:\Videos\H_1-2.mpg",
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

    private static BendSuggestionScanActions Aktionen(
        Func<CancellationToken, Task<IReadOnlyList<VideoSequenceFrame>>> extract,
        Func<VideoSequenceFrame, CancellationToken, Task<double?>> detect)
        => new(extract, detect);

    /// <summary>
    /// Bilder ab Sekunde 10 — die ersten Sekunden gelten ohne Meterstand als
    /// Schachteinfahrt und wuerden verworfen (siehe eigener Test dazu).
    /// </summary>
    private static IReadOnlyList<VideoSequenceFrame> Bilder(int anzahl)
        => Enumerable.Range(1, anzahl)
            .Select(index => new VideoSequenceFrame(index, 10 + index - 1, $"f{index:D6}.jpg"))
            .ToList();
}
