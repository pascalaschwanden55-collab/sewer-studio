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
/// Zweiter Entscheid 01.10.2026: Ab zwei belegten OSD-Metern gilt als Rate die gemessene
/// Geschwindigkeit zwischen den beiden letzten (belastbar: mindestens 1 s Abstand, 0 bis 5 m/s);
/// rueckwaerts oder Stillstand: Schaetzung bleibt am Anker.
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

    // ── Entscheid 01.10.2026: Rate aus den zwei letzten belegten OSD-Metern ──

    [Fact]
    public async Task Mit_zwei_belegten_OSD_Metern_gilt_die_gemessene_Geschwindigkeit()
    {
        // Bild 3 (t = 2 s) liest 1 m, Bild 6 (t = 5 s) liest 2,5 m: gemessen 0,5 m/s statt angenommen 5 m/s.
        var rec = await RunAsync(OsdJeAufruf(new Dictionary<int, double> { [3] = 1.0, [6] = 2.5 }));

        // Zwischen den beiden Ankern gibt es nur einen: angenommene Rate wie bisher.
        Assert.Equal(new[] { 6.0, 11.0 }, new[] { 4, 5 }.Select(i => Checkpoint(rec, i).Meter));
        Assert.Equal(2.5, Checkpoint(rec, 6).Meter);
        // Danach 2,5 m + 0,5 m/s seit t = 5 s, nicht 7,5 / 12,5 / 17,5 / 22,5 m.
        Assert.Equal(new[] { 3.0, 3.5, 4.0, 4.5 }, new[] { 7, 8, 9, 10 }.Select(i => Checkpoint(rec, i).Meter));
        Assert.All(new[] { 7, 8, 9, 10 }, i => Assert.Equal("LinearEstimate", Checkpoint(rec, i).MeterSource));
    }

    [Theory]
    [InlineData(5.0, 4.0)]   // rueckwaerts: 1 m in 2 s zurueck (plausibel), Schaetzung bleibt bei 4 m
    [InlineData(5.0, 5.0)]   // Stillstand
    public async Task Rueckwaerts_oder_Stillstand_bleibt_am_Anker(double ersterOsd, double zweiterOsd)
    {
        // Bild 4 (t = 3 s) und Bild 6 (t = 5 s) lesen den OSD-Meter.
        var rec = await RunAsync(OsdJeAufruf(new Dictionary<int, double> { [4] = ersterOsd, [6] = zweiterOsd }));

        Assert.All(new[] { 7, 8, 9, 10 }, i => Assert.Equal(zweiterOsd, Checkpoint(rec, i).Meter));
    }

    [Fact]
    public async Task Fortsetzung_aus_dem_Journal_behaelt_die_gemessene_Geschwindigkeit()
    {
        // Bild 8 (Kennbyte 7) ist leer: es uebernimmt den laufenden Meterstand.
        var leer = new HashSet<int> { 7 };
        var durchgehend = await RunAsync(OsdJeAufruf(new Dictionary<int, double> { [3] = 1.0, [6] = 2.5 }), leer);

        // Journal aus demselben Lauf bis Bild 7; der fortgesetzte Lauf liest keinen OSD-Meter mehr.
        var journal = Checkpoints(durchgehend).Where(c => c.FrameIndex <= 7).ToList();
        var fortgesetzt = await RunAsync(OsdJeAufruf(new Dictionary<int, double>()), leer,
            new AnalysisCheckpointState(7, journal));

        Assert.Equal(new[] { 3.0, 4.0, 4.5 }, new[] { 8, 9, 10 }.Select(i => Checkpoint(durchgehend, i).Meter));
        foreach (var i in new[] { 8, 9, 10 })
        {
            Assert.Equal(Checkpoint(durchgehend, i).Meter, Checkpoint(fortgesetzt, i).Meter);
            Assert.Equal(Checkpoint(durchgehend, i).MeterSource, Checkpoint(fortgesetzt, i).MeterSource);
        }
    }

    [Theory]
    // Gemessen: (2,5 - 1) m / (5 - 2) s = 0,5 m/s; angenommen waeren 5 m/s.
    [InlineData(8.0, 10.0, 2.5, 5.0, 1.0, 2.0, 4.0)]
    // Genau die Mindestzeit 1 s: gemessen 0,5 m/s gilt.
    [InlineData(7.0, 10.0, 2.0, 5.0, 1.5, 4.0, 3.0)]
    // Unter der Mindestzeit (0,5 s): angenommene Rate 5 m/s wie mit einem Anker.
    [InlineData(8.0, 10.0, 2.5, 5.0, 2.0, 4.5, 17.5)]
    // Genau 5 m/s gilt noch (angenommen waeren 0,5 m/s).
    [InlineData(6.0, 100.0, 6.0, 5.0, 1.0, 4.0, 11.0)]
    // Ueber 5 m/s (Rundungstoleranz der Folgepruefung): nicht belastbar, angenommene 0,5 m/s.
    [InlineData(6.0, 100.0, 6.01, 5.0, 1.0, 4.0, 6.51)]
    // Rueckwaerts und Stillstand: Rate 0, nie unter den Anker.
    [InlineData(8.0, 10.0, 4.0, 5.0, 5.0, 3.0, 4.0)]
    [InlineData(8.0, 10.0, 5.0, 5.0, 5.0, 3.0, 5.0)]
    public void Rate_aus_den_zwei_letzten_Ankern(double t, double dauerSek, double ankerMeter, double ankerZeit,
        double vorletzterMeter, double vorletzterZeit, double erwartet)
    {
        var geschaetzt = MultiModelMeterSchaetzung.Schaetze(t, dauerSek, haltungslaengeM: 50.0, laufenderMeter: 0.0,
            (ankerMeter, ankerZeit), (vorletzterMeter, vorletzterZeit));

        Assert.Equal(erwartet, geschaetzt, 9);
    }
}
