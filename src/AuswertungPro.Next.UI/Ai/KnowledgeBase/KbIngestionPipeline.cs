// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Phase 2.3: Channel-basierte Producer/Consumer-Pipeline fuer KB-Ingestion.
///
/// Audit B3 (Konsens 2/3): "SQLite-Schreib-Lock bremst Pipeline (Channel&lt;T&gt;
/// noetig)". Vier Stages, jeweils durch einen bounded Channel verbunden:
///
///   [Producer]  PreparedSample  ─►  [KI-Stage]  ScoredSample  ─►
///   [QualityGate]  GateChecked  ─►  [Writer]  WrittenResult
///
/// Die Channels haben Bounded-Capacity, damit ein langsamer Writer den
/// Producer/Klassifizierer drosselt (Backpressure) statt unbounded RAM
/// zu fressen. KB-Writes laufen ueber <see cref="KnowledgeBaseWriter"/>
/// (Phase 2.2) — d.h. selbst wenn der Writer sequenziell ist, bleibt
/// das KI-Stage und Quality-Gate nebenlaeufig.
///
/// Das Geruest ist generisch: Aufrufer geben pro Stage eine Lambda mit,
/// die das eigentliche Verhalten bestimmt. Damit kann es als Bibliothek
/// von <c>BatchSelfTrainingOrchestrator</c>, <c>TrainingCenterViewModel</c>
/// und <c>VideoSelfTrainingOrchestrator</c> verwendet werden — Aufrufer-
/// Migration ist eigene Phase und nicht Teil von 2.3.
/// </summary>
public sealed class KbIngestionPipeline
{
    private readonly KbIngestionPipelineOptions _opts;

    public KbIngestionPipeline(KbIngestionPipelineOptions? opts = null)
    {
        _opts = opts ?? new KbIngestionPipelineOptions();
    }

    /// <summary>
    /// Startet die Pipeline. Producer-Source liefert <see cref="PreparedSample"/>-
    /// Eintraege. Pipeline laeuft bis Source und alle Channels leer sind, dann
    /// werden die finalen <see cref="WrittenResult"/>-Eintraege zurueckgegeben.
    /// </summary>
    /// <param name="source">Async-Quelle (z.B. PDF-/Video-Extraktion).</param>
    /// <param name="embedAsync">KI-Stage: berechnet Embedding-Vektor.</param>
    /// <param name="gateAsync">QualityGate-Stage: pruefe Sample, gib Level zurueck.</param>
    /// <param name="writeAsync">Writer-Stage: persistiert Sample + Embedding.</param>
    /// <param name="ct">Cancellation-Token fuer alle Stages.</param>
    public async Task<KbIngestionResult> RunAsync(
        IAsyncEnumerable<PreparedSample> source,
        Func<PreparedSample, CancellationToken, Task<float[]?>> embedAsync,
        Func<ScoredSample, CancellationToken, Task<string>> gateAsync,
        Func<GateCheckedSample, CancellationToken, Task<bool>> writeAsync,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(embedAsync);
        ArgumentNullException.ThrowIfNull(gateAsync);
        ArgumentNullException.ThrowIfNull(writeAsync);

        var preparedCh = Channel.CreateBounded<PreparedSample>(_opts.PreparedCapacity);
        var scoredCh   = Channel.CreateBounded<ScoredSample>(_opts.ScoredCapacity);
        var gatedCh    = Channel.CreateBounded<GateCheckedSample>(_opts.GatedCapacity);

        var stats = new KbIngestionStats();

        // Stage 0: Producer feeds preparedCh
        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var sample in source.WithCancellation(ct).ConfigureAwait(false))
                {
                    Interlocked.Increment(ref stats._produced);
                    await preparedCh.Writer.WriteAsync(sample, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                preparedCh.Writer.Complete();
            }
        }, ct);

        // Stage 1: KI-Inferenz (parallel, max KiParallelism)
        var kiTasks = new List<Task>();
        for (var i = 0; i < _opts.KiParallelism; i++)
        {
            kiTasks.Add(Task.Run(async () =>
            {
                await foreach (var prepared in preparedCh.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    var vector = await embedAsync(prepared, ct).ConfigureAwait(false);
                    if (vector is null)
                    {
                        Interlocked.Increment(ref stats._kiFailed);
                        continue;
                    }
                    Interlocked.Increment(ref stats._kiOk);
                    await scoredCh.Writer.WriteAsync(
                        new ScoredSample(prepared.Sample, vector, prepared.SourceContext),
                        ct).ConfigureAwait(false);
                }
            }, ct));
        }
        var kiCompleter = Task.Run(async () =>
        {
            try { await Task.WhenAll(kiTasks).ConfigureAwait(false); }
            finally { scoredCh.Writer.Complete(); }
        }, ct);

        // Stage 2: QualityGate (sequenziell, weil meist CPU-leicht aber Reihenfolge-stabil)
        var gateTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var scored in scoredCh.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    var level = await gateAsync(scored, ct).ConfigureAwait(false);
                    Interlocked.Increment(ref stats._gateProcessed);
                    if (string.Equals(level, "Red", StringComparison.OrdinalIgnoreCase))
                    {
                        Interlocked.Increment(ref stats._gateRejected);
                        continue;
                    }
                    await gatedCh.Writer.WriteAsync(
                        new GateCheckedSample(scored.Sample, scored.Vector, level, scored.SourceContext),
                        ct).ConfigureAwait(false);
                }
            }
            finally
            {
                gatedCh.Writer.Complete();
            }
        }, ct);

        // Stage 3: Writer (sequenziell — KnowledgeBaseWriter serialisiert sowieso)
        var writerTask = Task.Run(async () =>
        {
            await foreach (var gated in gatedCh.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var ok = await writeAsync(gated, ct).ConfigureAwait(false);
                if (ok) Interlocked.Increment(ref stats._written);
                else    Interlocked.Increment(ref stats._writeFailed);
            }
        }, ct);

        await Task.WhenAll(producerTask, kiCompleter, gateTask, writerTask).ConfigureAwait(false);

        return new KbIngestionResult(
            Produced:     stats._produced,
            KiOk:         stats._kiOk,
            KiFailed:     stats._kiFailed,
            GateProcessed:stats._gateProcessed,
            GateRejected: stats._gateRejected,
            Written:      stats._written,
            WriteFailed:  stats._writeFailed);
    }
}

/// <summary>Konfiguration fuer Channel-Capacity und Parallelitaet.</summary>
public sealed record KbIngestionPipelineOptions(
    int PreparedCapacity = 32,
    int ScoredCapacity = 16,
    int GatedCapacity = 16,
    int KiParallelism = 4);

/// <summary>Stage-1-Eingabe: ein vorbereitetes Sample (PDF-Foto extrahiert, Video-Frame, ...).</summary>
public sealed record PreparedSample(TrainingSample Sample, string SourceContext = "");

/// <summary>Stage-2-Eingabe: Sample mit fertigem Embedding-Vektor.</summary>
public sealed record ScoredSample(TrainingSample Sample, float[] Vector, string SourceContext = "");

/// <summary>Stage-3-Eingabe: Sample nach QualityGate (Yellow/Green — Red wurde verworfen).</summary>
public sealed record GateCheckedSample(TrainingSample Sample, float[] Vector, string GateLevel, string SourceContext = "");

/// <summary>Endresultat einer Pipeline-Lauf-Statistik.</summary>
public sealed record KbIngestionResult(
    int Produced,
    int KiOk,
    int KiFailed,
    int GateProcessed,
    int GateRejected,
    int Written,
    int WriteFailed);

internal sealed class KbIngestionStats
{
    public int _produced;
    public int _kiOk;
    public int _kiFailed;
    public int _gateProcessed;
    public int _gateRejected;
    public int _written;
    public int _writeFailed;
}
