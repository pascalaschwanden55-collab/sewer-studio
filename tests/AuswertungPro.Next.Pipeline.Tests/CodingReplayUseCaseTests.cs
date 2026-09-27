using System.Security.Cryptography;
using AuswertungPro.Next.Application.UseCases.CodingReplay;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingReplayUseCaseTests
{
    private static readonly byte[] Image = [1, 2, 3];
    private static CodingReplayFrame Frame(string id = "a") =>
        new(id, Convert.ToHexString(SHA256.HashData(Image)), 12, 250, 40);
    private static CodingReplayActions Actions(List<CodingReplayResult> recorded, byte[]? image = null) =>
        new((_, _) => Task.FromResult(image ?? Image), (r, _) => { recorded.Add(r); return Task.CompletedTask; });

    [Fact]
    public async Task Bildzeitlimit_wird_gemeldet_und_der_naechste_Fall_laeuft()
    {
        var analyzer = new Analyzer(async (frame, _, ct) =>
        {
            if (frame.Id == "a") await Task.Delay(Timeout.Infinite, ct);
            return new CodingReplayObservation("NoDamage", [], new Dictionary<string, string>());
        });
        var result = await new CodingReplayUseCase(analyzer).RunAsync(
            [Frame(), Frame("b")], Actions([]), TimeSpan.FromMilliseconds(20));
        Assert.Equal(new[] { "timeout", "measured" }, result.Select(r => r.Status));
        Assert.Null(result[0].Observation);
    }

    [Fact]
    public async Task Geaendertes_Bild_erreicht_kein_Modell_und_zaehlt_nicht_negativ()
    {
        var analyzer = new Analyzer((_, _, _) => throw new Exception("Nicht aufrufen"));
        var recorded = new List<CodingReplayResult>();
        var result = await new CodingReplayUseCase(analyzer).RunAsync([Frame()], Actions(recorded, [9]), TimeSpan.FromSeconds(5));
        Assert.Equal(0, analyzer.Calls);
        Assert.Equal("technical_error", Assert.Single(result).Status);
        Assert.Null(result[0].Observation);
        Assert.Single(recorded);
    }

    [Fact]
    public async Task Fehlender_Kontext_erfindet_keine_300_mm_oder_50_m()
    {
        var analyzer = new Analyzer((_, _, _) => throw new Exception("Nicht aufrufen"));
        var result = await new CodingReplayUseCase(analyzer).RunAsync(
            [Frame() with { ReachLengthM = null }], Actions([]), TimeSpan.FromSeconds(5));
        Assert.Equal("context_missing", result[0].Status);
        Assert.Equal(0, analyzer.Calls);
    }

    [Fact]
    public async Task Einzelfehler_erhaelt_folgende_Messung_und_leere_gueltige_Antwort()
    {
        var analyzer = new Analyzer((f, _, _) => f.Id == "a"
            ? throw new IOException("Defekt")
            : Task.FromResult(new CodingReplayObservation("NoDamage", [], new Dictionary<string, string>())));
        var recorded = new List<CodingReplayResult>();
        var result = await new CodingReplayUseCase(analyzer).RunAsync([Frame(), Frame("b")], Actions(recorded), TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "technical_error", "measured" }, result.Select(r => r.Status));
        Assert.Empty(result[1].Observation!.Events);
        Assert.Equal(2, recorded.Count);
    }

    [Fact]
    public async Task Als_Wert_gelieferter_Modellfehler_bleibt_Fehler()
    {
        var analyzer = new Analyzer((_, _, _) => Task.FromResult(
            new CodingReplayObservation("ReviewRequired", [], new Dictionary<string, string>(), "DINO ausgefallen")));
        var result = await new CodingReplayUseCase(analyzer).RunAsync([Frame()], Actions([]), TimeSpan.FromSeconds(5));
        Assert.Equal("technical_error", result[0].Status);
    }

    [Fact]
    public async Task Abbruch_stoppt_ohne_erfundene_Ergebniszeile()
    {
        using var stop = new CancellationTokenSource();
        var recorded = new List<CodingReplayResult>();
        var analyzer = new Analyzer((_, _, ct) => { stop.Cancel(); ct.ThrowIfCancellationRequested(); throw new Exception(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CodingReplayUseCase(analyzer)
            .RunAsync([Frame(), Frame("b")], Actions(recorded), TimeSpan.FromSeconds(5), stop.Token));
        Assert.Empty(recorded);
        Assert.Equal(1, analyzer.Calls);
    }

    [Fact]
    public async Task Fehlender_Ausgabebeleg_stoppt_vor_naechstem_Bild()
    {
        var analyzer = new Analyzer((_, _, _) => Task.FromResult(new CodingReplayObservation("NoDamage", [], new Dictionary<string, string>())));
        await Assert.ThrowsAsync<IOException>(() => new CodingReplayUseCase(analyzer).RunAsync(
            [Frame(), Frame("b")], new((_, _) => Task.FromResult(Image), (_, _) => throw new IOException("Ablage voll")), TimeSpan.FromSeconds(5)));
        Assert.Equal(1, analyzer.Calls);
    }

    [Fact]
    public async Task Doppelte_Kennungen_stoppen_vor_erstem_Modellaufruf()
    {
        var analyzer = new Analyzer((_, _, _) => throw new Exception());
        await Assert.ThrowsAsync<ArgumentException>(() => new CodingReplayUseCase(analyzer)
            .RunAsync([Frame(), Frame()], Actions([]), TimeSpan.FromSeconds(5)));
        Assert.Equal(0, analyzer.Calls);
    }

    private sealed class Analyzer(Func<CodingReplayFrame, byte[], CancellationToken, Task<CodingReplayObservation>> action) : ICodingReplayAnalyzer
    {
        public int Calls { get; private set; }
        public Task<CodingReplayObservation> AnalyzeAsync(CodingReplayFrame f, byte[] bytes, CancellationToken ct)
        { Calls++; return action(f, bytes, ct); }
    }
}
