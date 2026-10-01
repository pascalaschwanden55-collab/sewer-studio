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
/// Entscheid Pascal 01.10.2026: Nach jedem uebernommenen (belegten) OSD-Meter zaehlt die lineare
/// Meterschaetzung der Mehrmodell-Analyse von dort aus weiter, mit derselben Rate wie die lineare
/// Schaetzung (angenommene Haltungslaenge je Videodauer). Ohne belegten OSD-Meter gilt die bisherige
/// Schaetzung. Ein verworfener OSD-Wert ist kein Anker; nach einer Fortsetzung aus dem Journal
/// ergibt sich derselbe Meter wie ohne Unterbrechung.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelMeterAnkerTests
{
    // 10 Bilder zu je 1 s, Haltungslaenge 50 m -> Rate 5 m/s; Bild k liegt bei t = k - 1.
    private const int Bilder = 10;

    public MultiModelMeterAnkerTests() => VsaResolverTestCatalog.ConfigureDefault();

    private static MultiModelAnalysisService Create(MultiModelSnapshotRecorder rec, ISet<int>? leer,
        EnhancedVisionAnalysisService qwen)
        => new(rec, new ScriptedVisionClient(rec), new PipelineConfig(true, new Uri("http://localhost:5001"), null,
                PipelineMode.MultiModel, 0.25, new Dictionary<string, double>(), 0.25, 0.20, 30, 300),
            "ffmpeg", qwen, rec,
            (_, _, _, _, ct) => SnapshotFrames.Source(Bilder, leer, null, ct),
            (_, _) => Task.FromResult((double)Bilder), new FixedPipelineOptions(), null, rec)
        {
            FrameStepSeconds = 1.0, UseClsPrefilter = false,
        };

    /// <summary>Qwen liest je Aufruf den angegebenen Meter; nicht angegebene Aufrufe lesen keinen.</summary>
    private static ScriptedQwenHandler OsdJeAufruf(IReadOnlyDictionary<int, double> meter) => new((call, _) =>
        ScriptedQwenHandler.Content(
            "{\"meter\":" + (meter.TryGetValue(call, out var m) ? m.ToString(CultureInfo.InvariantCulture) : "null")
            + ",\"findings\":[{\"label\":\"crack\",\"vsa_code_hint\":\"BABBA\",\"severity\":3}],"
            + "\"image_quality\":\"gut\",\"is_empty_frame\":false}"));

    private static async Task<MultiModelSnapshotRecorder> RunAsync(ScriptedQwenHandler handler,
        ISet<int>? leer = null, AnalysisCheckpointState? resume = null)
    {
        var rec = new MultiModelSnapshotRecorder { OpenState = resume ?? AnalysisCheckpointState.Empty };
        using var http = new HttpClient(handler);
        using var ollama = new OllamaClient(new Uri("http://localhost:11434"), http);
        var result = await Create(rec, leer, new EnhancedVisionAnalysisService(ollama, "test"))
            .AnalyzeAsync("dummy/video.mp4");
        Assert.True(result.IsSuccess, result.Error);
        return rec;
    }

    private static IReadOnlyList<AnalysisCheckpointFrame> Checkpoints(MultiModelSnapshotRecorder rec)
        => rec.Events.Where(e => e.StartsWith("CKPT {", StringComparison.Ordinal))
            .Select(e => JsonSerializer.Deserialize<AnalysisCheckpointFrame>(e[5..])!)
            .ToList();

    private static AnalysisCheckpointFrame Checkpoint(MultiModelSnapshotRecorder rec, int frameIndex)
        => Checkpoints(rec).Single(c => c.FrameIndex == frameIndex);

    private static PipelineTraceEntry Trace(MultiModelSnapshotRecorder rec, int frameIndex)
        => rec.Events.Where(e => e.StartsWith("TRACE {", StringComparison.Ordinal))
            .Select(e => JsonSerializer.Deserialize<PipelineTraceEntry>(e[6..])!)
            .Single(t => t.FrameIndex == frameIndex);

    [Fact]
    public async Task Schaetzung_zaehlt_nach_dem_uebernommenen_OSD_Meter_von_dort_aus_weiter()
    {
        // Bild 6 (t = 5 s, lineare Schaetzung 25 m) liest OSD 1 m; danach liest Qwen keinen Meter.
        var rec = await RunAsync(OsdJeAufruf(new Dictionary<int, double> { [6] = 1.0 }));

        Assert.Equal(20.0, Checkpoint(rec, 5).Meter);          // ohne Anker: bisherige Schaetzung
        Assert.Equal("LinearEstimate", Checkpoint(rec, 5).MeterSource);
        Assert.Equal(1.0, Checkpoint(rec, 6).Meter);
        Assert.Equal("QwenOsd", Checkpoint(rec, 6).MeterSource);

        // 1 m + 5 m/s seit t = 5 s, nicht mehr zurueck auf 30 / 35 / 40 / 45 m.
        Assert.Equal(new[] { 6.0, 11.0, 16.0, 21.0 }, new[] { 7, 8, 9, 10 }.Select(i => Checkpoint(rec, i).Meter));
        Assert.All(new[] { 7, 8, 9, 10 }, i =>
        {
            Assert.Equal("LinearEstimate", Checkpoint(rec, i).MeterSource);
            Assert.True(Checkpoint(rec, i).IsMeterEstimated);
        });
    }

    [Fact]
    public async Task Verworfener_OSD_Wert_ist_kein_Anker()
    {
        // Bild 6 liest 1 m (Anker), Bild 7 liest 40 m: 39 m in 1 s, verworfen.
        var rec = await RunAsync(OsdJeAufruf(new Dictionary<int, double> { [6] = 1.0, [7] = 40.0 }));

        Assert.Equal("OSD-Meter unplausibel: 40 m nach 1 m in 1 s", Trace(rec, 7).OsdMeterRejected);
        Assert.Equal(6.0, Checkpoint(rec, 7).Meter);
        Assert.Equal("LinearEstimate", Checkpoint(rec, 7).MeterSource);
        Assert.Equal(11.0, Checkpoint(rec, 8).Meter);          // weiter ab 1 m, nicht ab 40 m
    }

    [Fact]
    public async Task Fortsetzung_aus_dem_Journal_schaetzt_wie_ohne_Unterbrechung()
    {
        // Bild 8 (Kennbyte 7) ist leer: es uebernimmt den laufenden Meterstand.
        var leer = new HashSet<int> { 7 };
        var durchgehend = await RunAsync(OsdJeAufruf(new Dictionary<int, double> { [6] = 1.0 }), leer);

        // Journal aus demselben Lauf bis Bild 7; der fortgesetzte Lauf liest keinen OSD-Meter mehr.
        var journal = Checkpoints(durchgehend).Where(c => c.FrameIndex <= 7).ToList();
        var fortgesetzt = await RunAsync(OsdJeAufruf(new Dictionary<int, double>()), leer,
            new AnalysisCheckpointState(7, journal));

        Assert.Equal(new[] { 6.0, 16.0, 21.0 }, new[] { 8, 9, 10 }.Select(i => Checkpoint(durchgehend, i).Meter));
        foreach (var i in new[] { 8, 9, 10 })
        {
            Assert.Equal(Checkpoint(durchgehend, i).Meter, Checkpoint(fortgesetzt, i).Meter);
            Assert.Equal(Checkpoint(durchgehend, i).MeterSource, Checkpoint(fortgesetzt, i).MeterSource);
        }
    }

    [Theory]
    // Ohne Anker: bisherige Schaetzung, nie unter den laufenden Meterstand.
    [InlineData(3.0, 20.0, null, null, 20.0)]
    [InlineData(6.0, 20.0, null, null, 30.0)]
    // Mit Anker: Anker + Rate * Zeit seit dem Anker; ein hoeherer laufender Meterstand zaehlt nicht.
    [InlineData(8.0, 20.0, 1.0, 5.0, 16.0)]
    // Nie unter den Anker, auch bei einer Bildzeit vor der Ankerzeit.
    [InlineData(4.0, 0.0, 10.0, 5.0, 10.0)]
    public void Schaetzung_folgt_dem_Anker(double t, double laufend, double? ankerMeter, double? ankerZeit, double erwartet)
    {
        (double Meter, double ZeitSek)? anker = ankerMeter is { } m && ankerZeit is { } z ? (m, z) : null;

        var geschaetzt = MultiModelMeterSchaetzung.Schaetze(t, dauerSek: 10.0, haltungslaengeM: 50.0, laufend, anker);

        Assert.Equal(erwartet, geschaetzt, 9);
    }
}
