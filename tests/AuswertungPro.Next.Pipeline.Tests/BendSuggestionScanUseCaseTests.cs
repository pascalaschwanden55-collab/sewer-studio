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
                detect: (_, _) => Task.FromResult(BendFrameResult.Detected(0.9))),
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
                detect: (bild, _) => { gefragt.Add(bild.Index); return Task.FromResult(BendFrameResult.NoBend); }),
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
                detect: (bild, _) => Task.FromResult(bild.Index switch
                {
                    1 => BendFrameResult.Detected(0.30),
                    2 => BendFrameResult.Detected(0.55),
                    3 => BendFrameResult.Detected(0.85),
                    _ => BendFrameResult.NoBend
                })),
            CancellationToken.None);

        var einziger = Assert.Single(ergebnis.Suggestions);
        Assert.Equal(0.85, einziger.MaxConfidence, 3);
        Assert.Equal(BendSuggestionStrength.Strong, einziger.Strength);
        // Auch das schwache erste Bild gehoert zur Stelle: Der Arbeitspunkt gilt
        // fuer die Stelle als ganze, nicht fuer das einzelne Bild.
        Assert.Equal(3, einziger.FrameCount);
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
                        : Task.FromResult(BendFrameResult.NoBend)),
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
                detect: (_, _) => Task.FromResult(BendFrameResult.Detected(0.9))),
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
                detect: (_, _) => Task.FromResult(BendFrameResult.NoBend)) with { Now = uhr.Dequeue },
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
                    detect: (_, _) => Task.FromResult(BendFrameResult.NoBend)),
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
                     (_, _) => Task.FromResult(BendFrameResult.Detected(0.95))),
            CancellationToken.None);
        var mit = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(), Kalibrierung(),
            Aktionen(_ => Task.FromResult<IReadOnlyList<VideoSequenceFrame>>(spaet),
                     (_, _) => Task.FromResult(BendFrameResult.Detected(0.95))),
            CancellationToken.None);

        Assert.Empty(ohne.Suggestions);
        Assert.Single(mit.Suggestions);
        // Das Bild wurde trotzdem ausgewertet — verworfen wurde erst der Vorschlag.
        Assert.Equal(1, ohne.FramesAnalyzed);
    }

    [Fact]
    public async Task Ein_nicht_ausgewertetes_Bild_gilt_nie_als_kein_Bogen()
    {
        // Der Sidecar meldet ueber frame_usable, wenn ein Bild qualitaetsbedingt
        // nicht bewertet wurde. Das ist ein blinder Fleck, kein Negativbefund —
        // er wird gezaehlt und ausgewiesen.
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Kalibrierung(),
            Aktionen(
                extract: _ => Task.FromResult(Bilder(4)),
                detect: (bild, _) => Task.FromResult(bild.Index <= 2
                    ? BendFrameResult.NotAssessed("zu dunkel")
                    : BendFrameResult.NoBend)),
            CancellationToken.None);

        Assert.True(ergebnis.IsUsable);
        Assert.Equal(4, ergebnis.FramesAnalyzed);
        Assert.Equal(2, ergebnis.FramesNotAssessed);
        Assert.Empty(ergebnis.Suggestions);
    }

    [Fact]
    public async Task Die_Meterfolge_wird_erst_plausibilisiert_dann_gefuellt()
    {
        // Beleg vom 2026-08-08: Der Leser meldete 133,08 m in einer Haltung von
        // keinen 20 m. Die Sequenzpruefung muss diesen Wert verwerfen — sie
        // braucht dafuer die Folge ALLER Bilder, nicht nur die der Treffer.
        // Die Luecken zwischen den gelesenen 10,0 und 10,6 werden danach als
        // Schaetzung gefuellt: Sie ordnen zu, setzen aber keinen Ort.
        var meterJeIndex = new Dictionary<int, double?>
        {
            [1] = 10.0,
            [2] = 133.08,   // Fehllesung, unvertraeglich mit allen Nachbarn
            [3] = null,
            [4] = 10.6,
            [5] = null,
        };
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Kalibrierung(),
            Aktionen(
                extract: _ => Task.FromResult(Bilder(5)),
                detect: (bild, _) => Task.FromResult(
                    BendFrameResult.Detected(0.9, meterJeIndex[bild.Index]))),
            CancellationToken.None);

        // Ohne die Plausibilitaetspruefung oeffnete 133,08 eine eigene Stelle
        // (Meterabstand weit ueber der Zusammenfassungsgrenze von 1 m).
        var einziger = Assert.Single(ergebnis.Suggestions);
        Assert.Equal(10.0, einziger.MeterStart);
        Assert.True(einziger.MeterEnd.HasValue);
        Assert.Equal(10.6, einziger.MeterEnd!.Value, 3);
        Assert.Equal(5, einziger.FrameCount);
        // Gefuellte Luecken lagen im Bereich — der Vorschlag wird als teilweise
        // geschaetzt gekennzeichnet statt als rein gelesen.
        Assert.True(einziger.MeterIsEstimated);
    }

    [Fact]
    public async Task Die_Meterspur_traegt_jede_gelesene_oder_gefuellte_Sekunde()
    {
        // Der Codiermodus braucht am Rohrende den Meterstand — auch dort, wo kein
        // Bogen ist. Deshalb geht die ganze plausibilisierte, lueckengefuellte
        // Folge hinaus, nicht nur die Treffer.
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Kalibrierung(),
            Aktionen(
                extract: _ => Task.FromResult(Bilder(5)),
                detect: (bild, _) => Task.FromResult(bild.Index == 3
                    ? BendFrameResult.NoBend with { Meter = null }
                    : BendFrameResult.NoBend with { Meter = 0.5 * bild.Index })),
            CancellationToken.None);

        Assert.True(ergebnis.IsUsable);
        Assert.Equal(5, ergebnis.MeterTrack.Count);
        var dritte = ergebnis.MeterTrack.Single(p => p.TimeSeconds == 12.0);
        Assert.True(dritte.IsEstimated);
        Assert.Equal(1.5, dritte.Meter, 3);
        Assert.All(ergebnis.MeterTrack.Where(p => p.TimeSeconds != 12.0), p => Assert.False(p.IsEstimated));
    }

    [Fact]
    public async Task Ohne_Arbeitspunkt_ist_die_Meterspur_leer_und_nie_null()
    {
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            calibration: null,
            Aktionen(
                extract: _ => Task.FromResult(Bilder(2)),
                detect: (_, _) => Task.FromResult(BendFrameResult.NoBend)),
            CancellationToken.None);

        Assert.False(ergebnis.IsUsable);
        Assert.Empty(ergebnis.MeterTrack);
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
        Func<VideoSequenceFrame, CancellationToken, Task<BendFrameResult>> detect)
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
