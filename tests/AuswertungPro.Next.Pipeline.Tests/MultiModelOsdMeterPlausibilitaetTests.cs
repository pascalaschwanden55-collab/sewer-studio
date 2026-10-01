using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Entscheid Pascal 01.10.2026: Ein von Qwen gelesener OSD-Meter gilt in der Mehrmodell-Analyse
/// nur, wenn er mit hoechstens 5 m/s zum letzten belegten (uebernommenen) OSD-Meter passt.
/// Ein verworfener Wert setzt den laufenden Meterstand nicht zurueck; der Trace nennt ihn.
/// Der Anker ueberlebt auch die Fortsetzung aus dem Checkpoint-Journal.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelOsdMeterPlausibilitaetTests
{
    public MultiModelOsdMeterPlausibilitaetTests() => VsaResolverTestCatalog.ConfigureDefault();

    private static MultiModelAnalysisService Create(MultiModelSnapshotRecorder rec, int frames,
        EnhancedVisionAnalysisService qwen)
        => new(rec, new ScriptedVisionClient(rec), new PipelineConfig(true, new Uri("http://localhost:5001"), null,
                PipelineMode.MultiModel, 0.25, new Dictionary<string, double>(), 0.25, 0.20, 30, 300),
            "ffmpeg", qwen, rec,
            (_, _, _, _, ct) => SnapshotFrames.Source(frames, null, null, ct),
            (_, _) => Task.FromResult((double)frames), new FixedPipelineOptions(), null, rec)
        {
            FrameStepSeconds = 1.0, UseClsPrefilter = false,
        };

    private static ScriptedQwenHandler OsdMeters(params double[] meters) => new((call, _) => ScriptedQwenHandler.Content(
        "{\"meter\":" + meters[Math.Min(call, meters.Length) - 1].ToString(CultureInfo.InvariantCulture)
        + ",\"findings\":[{\"label\":\"crack\",\"vsa_code_hint\":\"BABBA\",\"severity\":3}],"
        + "\"image_quality\":\"gut\",\"is_empty_frame\":false}"));

    private static async Task<MultiModelSnapshotRecorder> RunAsync(int frames, ScriptedQwenHandler handler,
        AnalysisCheckpointState? resume = null)
    {
        var rec = new MultiModelSnapshotRecorder { OpenState = resume ?? AnalysisCheckpointState.Empty };
        using var http = new HttpClient(handler);
        using var ollama = new OllamaClient(new Uri("http://localhost:11434"), http);
        var result = await Create(rec, frames, new EnhancedVisionAnalysisService(ollama, "test"))
            .AnalyzeAsync("dummy/video.mp4");
        Assert.True(result.IsSuccess, result.Error);
        return rec;
    }

    private static AnalysisCheckpointFrame Checkpoint(MultiModelSnapshotRecorder rec, int frameIndex)
        => rec.Events.Where(e => e.StartsWith("CKPT {", StringComparison.Ordinal))
            .Select(e => JsonSerializer.Deserialize<AnalysisCheckpointFrame>(e[5..])!)
            .Single(c => c.FrameIndex == frameIndex);

    private static PipelineTraceEntry Trace(MultiModelSnapshotRecorder rec, int frameIndex)
        => rec.Events.Where(e => e.StartsWith("TRACE {", StringComparison.Ordinal))
            .Select(e => JsonSerializer.Deserialize<PipelineTraceEntry>(e[6..])!)
            .Single(t => t.FrameIndex == frameIndex);

    [Fact]
    public async Task Unplausibler_OSD_Sprung_setzt_den_Meterstand_nicht_zurueck()
    {
        // Bilder bei 0, 1, 2, 3 s; Schaetzung 0 / 12,5 / 25 / 37,5 m. OSD: 2 m (erster Wert),
        // 40 m (38 m in 1 s), 4 m (2 m in 2 s seit dem belegten Anker 2 m), danach weiter 4 m.
        var rec = await RunAsync(4, OsdMeters(2.0, 40.0, 4.0));

        var erstes = Checkpoint(rec, 1);
        Assert.Equal(2.0, erstes.Meter);
        Assert.Equal("QwenOsd", erstes.MeterSource);
        Assert.Null(Trace(rec, 1).OsdMeterRejected);

        var verworfen = Checkpoint(rec, 2);
        Assert.Equal(12.5, verworfen.Meter);                  // bisherige Schaetzung bleibt
        Assert.Equal("LinearEstimate", verworfen.MeterSource);
        Assert.True(verworfen.IsMeterEstimated);
        Assert.Equal("OSD-Meter unplausibel: 40 m nach 2 m in 1 s", Trace(rec, 2).OsdMeterRejected);
        Assert.Contains("LOG Debug Frame 2: OSD-Meter 40 verworfen (OSD-Meter unplausibel: 40 m nach 2 m in 1 s)", rec.Events);

        var drittes = Checkpoint(rec, 3);
        Assert.Equal(4.0, drittes.Meter);                     // passt zum belegten Anker, nicht zur Schaetzung
        Assert.Equal("QwenOsd", drittes.MeterSource);
        Assert.Null(Trace(rec, 3).OsdMeterRejected);
    }

    [Fact]
    public async Task Fortsetzung_aus_dem_Journal_uebernimmt_den_belegten_OSD_Anker()
    {
        // Journal: Bild 1 (t = 0 s) mit uebernommenem OSD 10 m, Bild 2 weitergeschaltet.
        var resume = new AnalysisCheckpointState(2,
        [
            new AnalysisCheckpointFrame(CheckpointFrameKind.Update, 1, 0.0, 10.0, "QwenOsd", false, null,
                Array.Empty<EnhancedFinding>()),
            new AnalysisCheckpointFrame(CheckpointFrameKind.Advance, 2, 1.0, 12.5, null, true, null,
                Array.Empty<EnhancedFinding>()),
        ]);

        // Bild 3 (t = 2 s) liest 40 m: 30 m in 2 s seit dem Anker. Bild 4 (t = 3 s) liest 14 m: passt.
        var rec = await RunAsync(4, OsdMeters(40.0, 14.0), resume);

        Assert.Equal("OSD-Meter unplausibel: 40 m nach 10 m in 2 s", Trace(rec, 3).OsdMeterRejected);
        Assert.Equal("LinearEstimate", Checkpoint(rec, 3).MeterSource);
        var viertes = Checkpoint(rec, 4);
        Assert.Equal(14.0, viertes.Meter);
        Assert.Equal("QwenOsd", viertes.MeterSource);
    }
}
