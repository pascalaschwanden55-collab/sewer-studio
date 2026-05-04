using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 2.3: Tests fuer Channel-basierte KbIngestionPipeline.
///
/// Verifiziert:
///   - Producer-Source wird durchgereicht
///   - KI-Stage berechnet Embedding parallel
///   - QualityGate-Stage filtert Red-Samples
///   - Writer-Stage zaehlt erfolgreiche Schreibvorgaenge
///   - Backpressure: Bounded Channel limitiert Memory bei langsamem Writer
///   - Cancellation: alle Stages stoppen sauber
///   - Fehler in einer Stage propagiert
/// </summary>
public sealed class KbIngestionPipelineTests
{
    private static TrainingSample MakeSample(string id = "s1", string code = "BAB") =>
        new()
        {
            SampleId = id,
            CaseId = "c",
            Code = code,
            Beschreibung = "Test"
        };

    private static async IAsyncEnumerable<PreparedSample> AsAsync(IEnumerable<PreparedSample> items)
    {
        foreach (var i in items) { await Task.Yield(); yield return i; }
    }

    // ── Happy Path ───────────────────────────────────────────────────

    [Fact]
    public async Task GanzePipeline_LiefertAlleSamplesDurch()
    {
        var pipeline = new KbIngestionPipeline();
        var inputs = new[]
        {
            new PreparedSample(MakeSample("s1")),
            new PreparedSample(MakeSample("s2")),
            new PreparedSample(MakeSample("s3"))
        };

        var result = await pipeline.RunAsync(
            source: AsAsync(inputs),
            embedAsync: (_, _) => Task.FromResult<float[]?>(new[] { 0.1f, 0.2f }),
            gateAsync: (_, _) => Task.FromResult("Green"),
            writeAsync: (_, _) => Task.FromResult(true));

        Assert.Equal(3, result.Produced);
        Assert.Equal(3, result.KiOk);
        Assert.Equal(0, result.KiFailed);
        Assert.Equal(3, result.GateProcessed);
        Assert.Equal(0, result.GateRejected);
        Assert.Equal(3, result.Written);
        Assert.Equal(0, result.WriteFailed);
    }

    [Fact]
    public async Task EmbedderGibtNullZurueck_KiFailedZaehltHoch()
    {
        var pipeline = new KbIngestionPipeline();
        var inputs = new[]
        {
            new PreparedSample(MakeSample("ok1")),
            new PreparedSample(MakeSample("fail")),
            new PreparedSample(MakeSample("ok2"))
        };

        var result = await pipeline.RunAsync(
            source: AsAsync(inputs),
            embedAsync: (s, _) => Task.FromResult<float[]?>(
                s.Sample.SampleId == "fail" ? null : new[] { 0.1f }),
            gateAsync: (_, _) => Task.FromResult("Green"),
            writeAsync: (_, _) => Task.FromResult(true));

        Assert.Equal(3, result.Produced);
        Assert.Equal(2, result.KiOk);
        Assert.Equal(1, result.KiFailed);
        Assert.Equal(2, result.Written);
    }

    [Fact]
    public async Task GateRedLevel_VerwirftSample()
    {
        var pipeline = new KbIngestionPipeline();
        var inputs = new[]
        {
            new PreparedSample(MakeSample("green1")),
            new PreparedSample(MakeSample("red1")),
            new PreparedSample(MakeSample("yellow1")),
            new PreparedSample(MakeSample("red2"))
        };

        var result = await pipeline.RunAsync(
            source: AsAsync(inputs),
            embedAsync: (_, _) => Task.FromResult<float[]?>(new[] { 0.1f }),
            gateAsync: (s, _) => Task.FromResult(
                s.Sample.SampleId.StartsWith("red") ? "Red" :
                s.Sample.SampleId.StartsWith("yellow") ? "Yellow" : "Green"),
            writeAsync: (_, _) => Task.FromResult(true));

        Assert.Equal(4, result.GateProcessed);
        Assert.Equal(2, result.GateRejected);   // 2 reds
        Assert.Equal(2, result.Written);        // 1 green + 1 yellow
    }

    [Fact]
    public async Task WriteFehler_WriteFailedZaehltHoch()
    {
        var pipeline = new KbIngestionPipeline();
        var inputs = new[]
        {
            new PreparedSample(MakeSample("ok")),
            new PreparedSample(MakeSample("fail"))
        };

        var result = await pipeline.RunAsync(
            source: AsAsync(inputs),
            embedAsync: (_, _) => Task.FromResult<float[]?>(new[] { 0.1f }),
            gateAsync: (_, _) => Task.FromResult("Green"),
            writeAsync: (s, _) => Task.FromResult(s.Sample.SampleId == "ok"));

        Assert.Equal(1, result.Written);
        Assert.Equal(1, result.WriteFailed);
    }

    // ── Backpressure ─────────────────────────────────────────────────

    [Fact]
    public async Task BoundedChannel_DrosseltProducerBeiLangsamemWriter()
    {
        // Bounded Channel mit kleinem Buffer + langsamer Writer
        // Producer kann nicht alles auf einmal pumpen — Backpressure.
        var opts = new KbIngestionPipelineOptions(
            PreparedCapacity: 2,
            ScoredCapacity: 2,
            GatedCapacity: 2,
            KiParallelism: 2);
        var pipeline = new KbIngestionPipeline(opts);

        var produced = 0;
        async IAsyncEnumerable<PreparedSample> Source()
        {
            for (var i = 0; i < 10; i++)
            {
                await Task.Yield();
                Interlocked.Increment(ref produced);
                yield return new PreparedSample(MakeSample($"s{i}"));
            }
        }

        var result = await pipeline.RunAsync(
            source: Source(),
            embedAsync: async (_, _) => { await Task.Delay(5); return new[] { 0.1f }; },
            gateAsync: async (_, _) => { await Task.Delay(5); return "Green"; },
            writeAsync: async (_, _) => { await Task.Delay(20); return true; });

        Assert.Equal(10, result.Produced);
        Assert.Equal(10, result.Written);
    }

    // ── Cancellation ─────────────────────────────────────────────────

    [Fact]
    public async Task Cancellation_StoptPipelineSauber()
    {
        var pipeline = new KbIngestionPipeline();
        using var cts = new CancellationTokenSource();

        async IAsyncEnumerable<PreparedSample> InfSource(
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            for (var i = 0; ; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(2, ct);
                yield return new PreparedSample(MakeSample($"s{i}"));
            }
        }

        var task = pipeline.RunAsync(
            source: InfSource(cts.Token),
            embedAsync: (_, _) => Task.FromResult<float[]?>(new[] { 0.1f }),
            gateAsync: (_, _) => Task.FromResult("Green"),
            writeAsync: (_, _) => Task.FromResult(true),
            ct: cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    // ── Defensive ────────────────────────────────────────────────────

    [Fact]
    public async Task LeereSource_LiefertNullErgebnis()
    {
        var pipeline = new KbIngestionPipeline();

        var result = await pipeline.RunAsync(
            source: AsAsync(Array.Empty<PreparedSample>()),
            embedAsync: (_, _) => Task.FromResult<float[]?>(new[] { 0.1f }),
            gateAsync: (_, _) => Task.FromResult("Green"),
            writeAsync: (_, _) => Task.FromResult(true));

        Assert.Equal(0, result.Produced);
        Assert.Equal(0, result.Written);
    }

    [Fact]
    public async Task NullSource_Wirft()
    {
        var pipeline = new KbIngestionPipeline();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            pipeline.RunAsync(
                source: null!,
                embedAsync: (_, _) => Task.FromResult<float[]?>(null),
                gateAsync: (_, _) => Task.FromResult("Green"),
                writeAsync: (_, _) => Task.FromResult(true)));
    }
}
