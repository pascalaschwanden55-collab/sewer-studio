using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Einzelne Bildausgaenge der Mehrmodell-Analyse, die vor AP05 keinen eigenen Test hatten:
/// ungueltiges (leeres) Bild und eine DINO-Antwort mit Einschraenkung (degraded).
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelFrameOutcomeTests
{
    public MultiModelFrameOutcomeTests() => VsaResolverTestCatalog.ConfigureDefault();

    private static MultiModelAnalysisService Create(MultiModelSnapshotRecorder rec, ScriptedVisionClient client,
        int frames, ISet<int>? emptyMarkers = null)
        => new(rec, client, new PipelineConfig(true, new Uri("http://localhost:5001"), null, PipelineMode.MultiModel,
                0.25, new Dictionary<string, double>(), 0.25, 0.20, 30, 300),
            "ffmpeg", null, rec,
            (_, _, _, _, ct) => SnapshotFrames.Source(frames, emptyMarkers, null, ct),
            (_, _) => Task.FromResult((double)frames), new FixedPipelineOptions(), null, rec)
        {
            FrameStepSeconds = 1.0, UseClsPrefilter = false,
        };

    private static List<AnalysisCheckpointFrame> Checkpoints(MultiModelSnapshotRecorder rec)
        => rec.Events.Where(e => e.StartsWith("CKPT {", StringComparison.Ordinal))
            .Select(e => System.Text.Json.JsonSerializer.Deserialize<AnalysisCheckpointFrame>(e[5..])!)
            .ToList();

    [Fact]
    public async Task Leeres_Bild_wird_regulaer_weitergeschaltet_ohne_Modellaufruf_und_ohne_Fehlerquote()
    {
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec);
        var result = await Create(rec, client, 4, emptyMarkers: new HashSet<int> { 1 }).AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.False(result.Degraded, result.DegradedReason);
        Assert.False(result.Incomplete);
        Assert.DoesNotContain(rec.Events, e => e.StartsWith("CALL", StringComparison.Ordinal) && e.EndsWith("m=1", StringComparison.Ordinal));
        var frame2 = Assert.Single(Checkpoints(rec), c => c.FrameIndex == 2);
        Assert.Equal(CheckpointFrameKind.Advance, frame2.Kind);
        Assert.Contains("CKPT-COMPLETE", rec.Events);
    }

    [Fact]
    public async Task Dino_mit_Einschraenkung_ist_kein_sauberer_Negativbefund_sondern_Wiederholung()
    {
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec)
        {
            Dino = m => m == 1
                ? new DinoResponse([], 1, Degraded: true, Error: "CUDA-Fehler (Test)", ErrorCode: "dino_oom")
                : ScriptedVisionClient.TwoBoxes(),
        };
        var result = await Create(rec, client, 4).AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        var frame2 = Assert.Single(Checkpoints(rec), c => c.FrameIndex == 2);
        Assert.Equal(CheckpointFrameKind.RetryRequired, frame2.Kind);
        Assert.Contains(rec.Events, e => e.StartsWith("TRACE", StringComparison.Ordinal)
                                         && e.Contains("\"Path\":\"dino_degraded\"", StringComparison.Ordinal)
                                         && e.Contains("\"DegradedReason\":\"dino_oom\"", StringComparison.Ordinal));
        Assert.DoesNotContain(rec.Events, e => e.Contains("dino_no_boxes", StringComparison.Ordinal));
        // Ein Fehlerframe von vier liegt ueber der Skip-Quote von 10 %; der Checkpoint bleibt offen.
        Assert.True(result.Incomplete);
        Assert.DoesNotContain("CKPT-COMPLETE", rec.Events);
        Assert.DoesNotContain(rec.Events, e => e.Contains("CALL sam m=1", StringComparison.Ordinal));
    }
}
