using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Vorabdurchlauf fuer Rohranfang und Rohrende: Bilder einmal holen, je
/// freigegebener Lernstufe jedes Bild fragen, je Klasse hoechstens eine Stelle
/// vorschlagen. Aussenverbindungen sind eingehaengt, damit die Regeln ohne ffmpeg
/// und ohne Sidecar pruefbar bleiben.
/// </summary>
public sealed class PipeEndSuggestionScanUseCaseTests
{
    [Fact]
    public async Task Jedes_Bild_wird_je_Klasse_genau_einmal_und_klassenweise_nacheinander_gefragt()
    {
        // Beide Lernstufen teilen sich im Sidecar denselben Modellplatz. Wechselte der
        // Lauf je Bild die Klasse, wuerde das Gewicht bei jedem Bild neu geladen.
        var gefragt = new List<(PipeEndKind Kind, int Index)>();
        var ergebnis = await PipeEndSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            PipeEndLernstufePins.All,
            Aktionen(
                extract: _ => Task.FromResult(Bilder(3)),
                score: (bild, pin, _) => { gefragt.Add((pin.Kind, bild.Index)); return Task.FromResult(0.0); }),
            CancellationToken.None);

        Assert.Equal(
            new[]
            {
                (PipeEndKind.Rohranfang, 1), (PipeEndKind.Rohranfang, 2), (PipeEndKind.Rohranfang, 3),
                (PipeEndKind.Rohrende, 1), (PipeEndKind.Rohrende, 2), (PipeEndKind.Rohrende, 3)
            },
            gefragt);
        Assert.Equal(3, ergebnis.FramesAnalyzed);
        Assert.Empty(ergebnis.Suggestions);
    }

    [Fact]
    public async Task Die_staerkste_Stelle_je_Klasse_wird_zum_Vorschlag()
    {
        var ergebnis = await PipeEndSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            PipeEndLernstufePins.All,
            Aktionen(
                extract: _ => Task.FromResult(Bilder(60)),
                score: (bild, pin, _) => Task.FromResult(pin.Kind switch
                {
                    PipeEndKind.Rohranfang => bild.TimeSeconds <= 2 ? 0.95 : 0.02,
                    _ => bild.TimeSeconds is >= 40 and <= 42 ? 0.90 : 0.03
                })),
            CancellationToken.None);

        Assert.Equal(2, ergebnis.Suggestions.Count);
        var anfang = ergebnis.Suggestions[0];
        var ende = ergebnis.Suggestions[1];
        Assert.Equal(PipeEndKind.Rohranfang, anfang.Kind);
        Assert.Equal(0.0, anfang.TimeStartSeconds);
        Assert.Equal(2.0, anfang.TimeEndSeconds);
        Assert.Equal(PipeEndKind.Rohrende, ende.Kind);
        Assert.Equal(40.0, ende.TimeStartSeconds);
        Assert.Equal(42.0, ende.TimeEndSeconds);
        Assert.Equal(0.90, ende.MaxConfidence, 3);
    }

    [Fact]
    public async Task Beim_Rohrende_zaehlt_der_Schacht_am_Anfang_nicht_beim_Rohranfang_schon()
    {
        // Dieselbe starke Meldung in den ersten drei Sekunden: fuer den Rohranfang
        // ist sie der Treffer, fuer das Rohrende der ausgeblendete Schacht.
        var ergebnis = await PipeEndSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            PipeEndLernstufePins.All,
            Aktionen(
                extract: _ => Task.FromResult(Bilder(10)),
                score: (bild, _, _) => Task.FromResult(bild.TimeSeconds <= 2 ? 0.99 : 0.01)),
            CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Suggestions);
        Assert.Equal(PipeEndKind.Rohranfang, vorschlag.Kind);
    }

    [Fact]
    public async Task Ohne_Treffer_bleibt_die_Liste_leer_und_der_Lauf_ist_gueltig()
    {
        var ergebnis = await PipeEndSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            PipeEndLernstufePins.All,
            Aktionen(
                extract: _ => Task.FromResult(Bilder(5)),
                score: (_, _, _) => Task.FromResult(0.2)),
            CancellationToken.None);

        Assert.Empty(ergebnis.Suggestions);
        Assert.Equal(5, ergebnis.FramesAnalyzed);
        Assert.Equal(PipeEndLernstufePins.All, ergebnis.Pins);
    }

    [Fact]
    public async Task Ein_technischer_Fehler_wird_geworfen_und_nie_als_kein_Treffer_gewertet()
    {
        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PipeEndSuggestionScanUseCase.ExecuteAsync(
                Auftrag(),
                PipeEndLernstufePins.All,
                Aktionen(
                    extract: _ => Task.FromResult(Bilder(3)),
                    score: (_, _, _) => throw new InvalidOperationException("Sidecar weg")),
                CancellationToken.None));

        Assert.Equal("Sidecar weg", fehler.Message);
    }

    [Fact]
    public async Task Ein_Abbruch_stoppt_den_Lauf()
    {
        using var abbruch = new CancellationTokenSource();
        var gefragt = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PipeEndSuggestionScanUseCase.ExecuteAsync(
                Auftrag(),
                PipeEndLernstufePins.All,
                Aktionen(
                    extract: _ => Task.FromResult(Bilder(10)),
                    score: (_, _, _) =>
                    {
                        if (++gefragt == 2)
                            abbruch.Cancel();
                        return Task.FromResult(0.0);
                    }),
                abbruch.Token));

        Assert.True(gefragt < 20, "Nach dem Abbruch darf kein weiteres Bild gefragt werden.");
    }

    [Fact]
    public async Task Der_Fortschritt_wird_je_Klasse_gemeldet()
    {
        var meldungen = new List<(PipeEndKind Kind, int Verarbeitet, int Gesamt)>();
        await PipeEndSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            PipeEndLernstufePins.All,
            Aktionen(
                extract: _ => Task.FromResult(Bilder(2)),
                score: (_, _, _) => Task.FromResult(0.0)) with
            {
                ReportProgress = (kind, verarbeitet, gesamt) => meldungen.Add((kind, verarbeitet, gesamt))
            },
            CancellationToken.None);

        Assert.Equal(
            new[]
            {
                (PipeEndKind.Rohranfang, 1, 2), (PipeEndKind.Rohranfang, 2, 2),
                (PipeEndKind.Rohrende, 1, 2), (PipeEndKind.Rohrende, 2, 2)
            },
            meldungen);
    }

    [Fact]
    public async Task Laufzeit_und_gepinnte_Lernstufen_stehen_im_Ergebnis()
    {
        var uhr = new DateTimeOffset(2026, 9, 4, 14, 0, 0, TimeSpan.Zero);
        var aufrufe = 0;
        var ergebnis = await PipeEndSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            PipeEndLernstufePins.All,
            Aktionen(
                extract: _ => Task.FromResult(Bilder(1)),
                score: (_, _, _) => Task.FromResult(0.0)) with
            {
                Now = () => uhr.AddSeconds(30 * aufrufe++)
            },
            CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(30), ergebnis.Duration);
        Assert.Contains(ergebnis.Pins, pin => pin.Kind == PipeEndKind.Rohranfang && pin.Klasse == "rohranfang");
        Assert.Contains(ergebnis.Pins, pin => pin.Kind == PipeEndKind.Rohrende && pin.Klasse == "rohrende");
    }

    [Fact]
    public void Die_Pins_tragen_die_freigegebenen_Gewichte_und_Messwerte()
    {
        // Die Werte stammen aus C:\KI_BRAIN\training\lernstufen\freigaben\*.json
        // (2026-08-12). Ein anderes Gewicht braucht eine neue Freigabe und neue Zahlen.
        var anfang = PipeEndLernstufePins.Rohranfang;
        var ende = PipeEndLernstufePins.Rohrende;

        Assert.Equal("40b0315aabc43095c61b196e5bf6011fb2123b7f99a2ccc3ce4a75ca6b910d9b", anfang.WeightSha256);
        Assert.Equal(0.8545, anfang.Precision, 4);
        Assert.Equal(0.9783, anfang.Recall, 4);
        Assert.Equal("fb70e77ce5e3676ac1376c17f1bdfdf208f15c8010f3fa720d395aab7a95a4f2", ende.WeightSha256);
        Assert.Equal(0.8889, ende.Precision, 4);
        Assert.Equal(0.8837, ende.Recall, 4);
    }

    private static PipeEndScanRequest Auftrag()
        => new() { VideoPath = @"D:\Haltungen\H_36053-36052.mpg" };

    private static PipeEndScanActions Aktionen(
        Func<CancellationToken, Task<IReadOnlyList<VideoSequenceFrame>>> extract,
        Func<VideoSequenceFrame, PipeEndLernstufePin, CancellationToken, Task<double>> score)
        => new(extract, score);

    /// <summary>Bilder mit 1 Bild je Sekunde, Index ab 1, Zeit ab 0 s (wie der Extraktor).</summary>
    private static IReadOnlyList<VideoSequenceFrame> Bilder(int anzahl)
        => Enumerable.Range(0, anzahl)
            .Select(i => new VideoSequenceFrame(i + 1, i, $@"C:\tmp\f{i + 1:000000}.jpg"))
            .ToList();
}
