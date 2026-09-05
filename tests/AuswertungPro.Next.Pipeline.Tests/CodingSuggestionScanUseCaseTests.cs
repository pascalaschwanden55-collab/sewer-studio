using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Vorabdurchlauf des Codiermodus: Bogen zuerst, dann Anfang/Ende; jeder
/// Teil faellt fuer sich aus; ein Abbruch geht durch; das Sitzungsgedaechtnis
/// wird nur bei mindestens einem Vorschlag gesetzt.
/// </summary>
public sealed class CodingSuggestionScanUseCaseTests
{
    [Fact]
    public async Task Bogen_laeuft_vor_Anfang_und_Ende_und_der_Pin_ist_gesetzt()
    {
        var reihenfolge = new List<string>();
        string? kandidat = null;

        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (r, _) => { reihenfolge.Add("bogen"); kandidat = r.CandidateId; return Task.FromResult(BogenOk()); },
                pipeEnds: (_, _) => { reihenfolge.Add("enden"); return Task.FromResult(EndenOk()); }),
            CancellationToken.None);

        Assert.Equal(new[] { "bogen", "enden" }, reihenfolge);
        Assert.Equal(CodingBendCandidatePin.Id, kandidat);
        Assert.Equal(3, set.Suggestions.Count);
        Assert.Equal(CodingSuggestionPartStatus.Bereit, set.BogenTeil.Status);
        Assert.Equal(CodingSuggestionPartStatus.Bereit, set.AnfangEndeTeil.Status);
    }

    [Fact]
    public async Task Ausgeschaltet_startet_nichts()
    {
        var aufgerufen = false;
        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag() with { Enabled = false },
            Aktionen(
                bends: (_, _) => { aufgerufen = true; return Task.FromResult(BogenOk()); },
                pipeEnds: (_, _) => { aufgerufen = true; return Task.FromResult(EndenOk()); }),
            CancellationToken.None);

        Assert.False(aufgerufen);
        Assert.Empty(set.Suggestions);
        Assert.Equal(CodingSuggestionPartStatus.NichtVerfuegbar, set.BogenTeil.Status);
    }

    [Fact]
    public async Task Ein_Bogen_ohne_Arbeitspunkt_laesst_Anfang_und_Ende_trotzdem_laufen()
    {
        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenNichtNutzbar("kein Arbeitspunkt")),
                pipeEnds: (_, _) => Task.FromResult(EndenOk())),
            CancellationToken.None);

        Assert.Equal(CodingSuggestionPartStatus.NichtVerfuegbar, set.BogenTeil.Status);
        Assert.Equal("kein Arbeitspunkt", set.BogenTeil.Grund);
        Assert.Equal(2, set.Suggestions.Count);
        Assert.Empty(set.MeterTrack);
    }

    [Fact]
    public async Task Ein_technischer_Fehler_wird_Fehler_und_nie_eine_leere_Liste()
    {
        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenOk()),
                pipeEnds: (_, _) => throw new InvalidOperationException("Sidecar nicht erreichbar")),
            CancellationToken.None);

        Assert.Equal(CodingSuggestionPartStatus.Fehler, set.AnfangEndeTeil.Status);
        Assert.Contains("Sidecar nicht erreichbar", set.AnfangEndeTeil.Grund);
        Assert.Single(set.Suggestions);
        Assert.Equal(CodingSuggestionKind.Bogen, set.Suggestions[0].Kind);
    }

    [Fact]
    public async Task Ein_Abbruch_wird_durchgereicht_und_markiert_nichts()
    {
        var markiert = false;
        using var quelle = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CodingSuggestionScanUseCase.ExecuteAsync(
                Auftrag(),
                Aktionen(
                    bends: (_, ct) => { quelle.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(BogenOk()); },
                    pipeEnds: (_, _) => Task.FromResult(EndenOk()),
                    markExposed: _ => markiert = true),
                quelle.Token));

        Assert.False(markiert);
    }

    [Fact]
    public async Task Das_Gedaechtnis_wird_nur_bei_mindestens_einem_Vorschlag_gesetzt()
    {
        var markierte = new List<string>();

        await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenOk() with { Suggestions = Array.Empty<BendSuggestion>() }),
                pipeEnds: (_, _) => Task.FromResult(EndenOk() with { Suggestions = Array.Empty<PipeEndSuggestion>() }),
                markExposed: markierte.Add),
            CancellationToken.None);
        Assert.Empty(markierte);

        await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenOk()),
                pipeEnds: (_, _) => Task.FromResult(EndenOk()),
                markExposed: markierte.Add),
            CancellationToken.None);
        Assert.Equal(new[] { "H_1-2" }, markierte);
    }

    [Fact]
    public async Task Anfang_und_Ende_tragen_den_gepinnten_Abnahmewert_und_die_Meterspur_kommt_vom_Bogen()
    {
        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenOk()),
                pipeEnds: (_, _) => Task.FromResult(EndenOk())),
            CancellationToken.None);

        var anfang = Assert.Single(set.Suggestions, s => s.Kind == CodingSuggestionKind.Rohranfang);
        Assert.Equal(PipeEndLernstufePins.Rohranfang.Precision, anfang.AcceptancePrecision);
        Assert.Equal(2, set.MeterTrack.Count);
    }

    [Fact]
    public void Der_Fortschritt_teilt_sich_in_zwei_Haelften()
    {
        Assert.Equal(0, CodingSuggestionScanUseCase.Percent(bogenPhase: true, 0, 100));
        Assert.Equal(25, CodingSuggestionScanUseCase.Percent(bogenPhase: true, 50, 100));
        Assert.Equal(50, CodingSuggestionScanUseCase.Percent(bogenPhase: false, 0, 100));
        Assert.Equal(100, CodingSuggestionScanUseCase.Percent(bogenPhase: false, 100, 100));
        Assert.Equal(0, CodingSuggestionScanUseCase.Percent(bogenPhase: true, 5, 0));
    }

    private static CodingSuggestionScanRequest Auftrag()
        => new(@"D:\Videos\H_1-2.mpg", "H_1-2", Enabled: true);

    private static CodingSuggestionScanActions Aktionen(
        Func<BendSuggestionScanRequest, CancellationToken, Task<BendSuggestionScanResult>> bends,
        Func<PipeEndScanRequest, CancellationToken, Task<PipeEndScanResult>> pipeEnds,
        Action<string>? markExposed = null)
        => new(bends, pipeEnds, markExposed ?? (_ => { }));

    private static BendSuggestionScanResult BogenOk()
        => new(
            true, string.Empty,
            [new BendSuggestion(9.42, 9.42, 30.0, 0.9, 4, BendSuggestionStrength.Strong)],
            60, 0, TimeSpan.FromSeconds(5),
            CodingBendCandidatePin.Id, CodingBendCandidatePin.WeightSha256, 0.5, 0.8, "Test",
            [new MeterTrackPoint(29.0, 9.0, false), new MeterTrackPoint(30.0, 9.42, false)]);

    private static BendSuggestionScanResult BogenNichtNutzbar(string grund)
        => new(false, grund, Array.Empty<BendSuggestion>(), 0, 0, TimeSpan.Zero,
            CodingBendCandidatePin.Id, CodingBendCandidatePin.WeightSha256, 0.0, 0.0);

    private static PipeEndScanResult EndenOk()
        => new(
            [
                new PipeEndSuggestion(PipeEndKind.Rohranfang, 3.0, 5.0, 4.0, 0.97, 3),
                new PipeEndSuggestion(PipeEndKind.Rohrende, 141.0, 145.0, 143.0, 0.91, 5)
            ],
            60, TimeSpan.FromSeconds(6), PipeEndLernstufePins.All);
}
