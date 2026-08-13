using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.PipeEndpoints;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class PipeEndpointScanUseCaseTests
{
    private static readonly PipeEndpointClass Anfang = new("rohranfang", new string('a', 64), 0.855, 0.978);
    private static readonly PipeEndpointClass Ende = new("rohrende", new string('b', 64), 0.889, 0.884);

    private static IReadOnlyList<VideoSequenceFrame> Frames(int anzahl)
        => Enumerable.Range(0, anzahl)
            .Select(i => new VideoSequenceFrame(i, i, $"f{i:D4}.jpg"))
            .ToArray();

    private static PipeEndpointScanActions Actions(
        IReadOnlyList<VideoSequenceFrame> frames,
        Func<VideoSequenceFrame, PipeEndpointClass, PipeEndpointFrameResult> antwort)
        => new(_ => Task.FromResult(frames),
               (frame, klasse, _) => Task.FromResult(antwort(frame, klasse)));

    private static PipeEndpointScanRequest Request(params PipeEndpointClass[] klassen)
        => new() { VideoPath = @"D:\Haltungen\x\video.mp4", Classes = klassen };

    [Fact]
    public async Task Meldet_je_Klasse_genau_die_staerkste_Stelle()
    {
        // Zwei Erhebungen je Klasse; die schwaechere darf nicht gewinnen.
        var actions = Actions(Frames(10), (frame, klasse) => klasse.Klasse switch
        {
            "rohranfang" => PipeEndpointFrameResult.Assessed(frame.Index switch { 1 => 0.90, 2 => 0.99, _ => 0.10 }),
            _ => PipeEndpointFrameResult.Assessed(frame.Index switch { 7 => 0.95, 8 => 0.60, _ => 0.05 }),
        });

        var result = await PipeEndpointScanUseCase.ExecuteAsync(
            Request(Anfang, Ende), actions, CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Empty(result.NotFound);
        var anfang = result.Suggestions.Single(s => s.Klasse == "rohranfang");
        var ende = result.Suggestions.Single(s => s.Klasse == "rohrende");
        Assert.Equal(2, anfang.FrameIndex);
        Assert.Equal(0.99, anfang.Confidence, 3);
        Assert.Equal(7, ende.FrameIndex);
        Assert.Equal(0.95, ende.Confidence, 3);
    }

    [Fact]
    public async Task Kein_Zeitfenster_die_Stelle_darf_ueberall_liegen()
    {
        // Beim Rohrende laeuft die Aufnahme nach dem Zielschacht oft weiter oder
        // bricht vorher ab. Ein Fenster kostete am 2026-08-12 30 Punkte Recall.
        var actions = Actions(Frames(300), (frame, _) =>
            PipeEndpointFrameResult.Assessed(frame.Index == 120 ? 0.97 : 0.05));

        var result = await PipeEndpointScanUseCase.ExecuteAsync(
            Request(Ende), actions, CancellationToken.None);

        Assert.Equal(120, result.Suggestions.Single().FrameIndex);
    }

    [Fact]
    public async Task Ohne_Treffer_gilt_nicht_gefunden_statt_nicht_vorhanden()
    {
        // In rund jedem fuenften Video ist gar keine Einfahrt zu sehen.
        var actions = Actions(Frames(20), (_, _) => PipeEndpointFrameResult.Assessed(0.20));

        var result = await PipeEndpointScanUseCase.ExecuteAsync(
            Request(Anfang), actions, CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Empty(result.Suggestions);
        Assert.Equal(new[] { "rohranfang" }, result.NotFound);
    }

    [Fact]
    public async Task Nicht_bewertete_Bilder_zaehlen_nicht_als_nicht_sichtbar()
    {
        var actions = Actions(Frames(10), (frame, _) => frame.Index < 5
            ? PipeEndpointFrameResult.NotAssessed("Bild unlesbar")
            : PipeEndpointFrameResult.Assessed(0.80));

        var result = await PipeEndpointScanUseCase.ExecuteAsync(
            Request(Anfang), actions, CancellationToken.None);

        Assert.Equal(5, result.FramesNotAssessed);
        Assert.Equal(5, result.Suggestions.Single().FrameIndex);
    }

    [Fact]
    public async Task Ein_Modellfehler_bricht_ab_und_wird_nie_zu_nicht_sichtbar()
    {
        var actions = new PipeEndpointScanActions(
            _ => Task.FromResult(Frames(10)),
            (_, _, _) => throw new InvalidOperationException("Sidecar weg"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PipeEndpointScanUseCase.ExecuteAsync(Request(Anfang), actions, CancellationToken.None));
    }

    [Theory]
    [InlineData("", "aaaa")]
    [InlineData("rohranfang", "")]
    public async Task Ohne_vollstaendigen_Pin_laeuft_nichts(string klasse, string sha)
    {
        var aufgerufen = false;
        var actions = new PipeEndpointScanActions(
            _ => { aufgerufen = true; return Task.FromResult(Frames(5)); },
            (_, _, _) => Task.FromResult(PipeEndpointFrameResult.Assessed(0.99)));

        var result = await PipeEndpointScanUseCase.ExecuteAsync(
            Request(new PipeEndpointClass(klasse, sha, 0.8, 0.8)), actions, CancellationToken.None);

        Assert.False(result.IsUsable);
        Assert.False(aufgerufen);   // nicht einmal die Bildextraktion darf starten
    }

    [Fact]
    public async Task Dieselbe_Klasse_zweimal_wird_abgewiesen()
    {
        var result = await PipeEndpointScanUseCase.ExecuteAsync(
            Request(Anfang, Anfang),
            Actions(Frames(3), (_, _) => PipeEndpointFrameResult.Assessed(0.9)),
            CancellationToken.None);

        Assert.False(result.IsUsable);
    }

    [Fact]
    public async Task Ohne_Bilder_ist_das_Ergebnis_unbrauchbar_nicht_leer()
    {
        var result = await PipeEndpointScanUseCase.ExecuteAsync(
            Request(Anfang),
            Actions(Array.Empty<VideoSequenceFrame>(), (_, _) => PipeEndpointFrameResult.Assessed(0.9)),
            CancellationToken.None);

        Assert.False(result.IsUsable);
        Assert.Contains("Bilder", result.Reason);
    }

    [Fact]
    public async Task Der_Abbruch_des_Benutzers_wird_durchgereicht()
    {
        using var cts = new CancellationTokenSource();
        var actions = new PipeEndpointScanActions(
            _ => Task.FromResult(Frames(10)),
            (frame, _, token) =>
            {
                if (frame.Index == 3) cts.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(PipeEndpointFrameResult.Assessed(0.9));
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PipeEndpointScanUseCase.ExecuteAsync(Request(Anfang), actions, cts.Token));
    }

    [Fact]
    public async Task Die_gemessene_Guete_reist_mit_dem_Vorschlag()
    {
        var result = await PipeEndpointScanUseCase.ExecuteAsync(
            Request(Anfang),
            Actions(Frames(3), (_, _) => PipeEndpointFrameResult.Assessed(0.9)),
            CancellationToken.None);

        var v = result.Suggestions.Single();
        Assert.Equal(0.855, v.Precision, 3);
        Assert.Equal(0.978, v.Recall, 3);
    }
}
