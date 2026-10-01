using System.Net;
using System.Net.Http;
using System.Text;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Audit A03 (23.09.2026): Ein zu dunkles, zu helles, strukturloses oder unscharfes Bild darf nie gruen als
/// «Kein Schaden erkannt» erscheinen. Mit freigegebenem YOLO ging der Qualitaetsgrund bisher verloren; heute
/// (YOLO nicht freigegeben) erscheint das Bild orange, nennt aber nur den Detektor statt «zu dunkel».
/// </summary>
public sealed class SingleFrameBildqualitaetTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private sealed class Handler(bool qualifiziert, string frameClass, bool nutzbar, string qualitaet) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = (request.RequestUri?.AbsolutePath ?? "") switch
            {
                "/health" => $$"""
                    { "status": "ok", "version": "1.2.0", "gpu": null,
                      "detector_qualification": { "qualified": {{(qualifiziert ? "true" : "false")}}, "reason": null, "artifact": { "sha256": "{{Sha}}" } } }
                    """,
                "/classify/yolo" => $$"""
                    { "predictions": [], "inference_time_ms": 3, "usable": {{(nutzbar ? "true" : "false")}},
                      "quality_reason": "{{qualitaet}}", "model_name": "m", "model_source": "active.json", "classifier_loaded": true }
                    """,
                "/detect/yolo" => $$"""
                    { "is_relevant": false, "detections": [], "frame_class": "{{frameClass}}", "inference_time_ms": 4,
                      "detector_qualified": true, "detector_artifact_sha256": "{{Sha}}" }
                    """,
                "/detect/dino" => """{ "detections": [], "inference_time_ms": 7 }""",
                var pfad => throw new InvalidOperationException($"Unerwarteter Endpunkt: {pfad}"),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static Task<SingleFrameResult> Analysiere(Handler h)
        => new SingleFrameMultiModelService(new VisionPipelineClient(
                new Uri("http://127.0.0.1:8100"), new HttpClient(h), sidecarToken: "test-token"))
            .AnalyzeFrameAsync([1, 2, 3], pipeDiameterMm: 300, calibration: null, currentMeterM: 5.0, reachLengthM: 40.0);

    [Theory]
    [InlineData("too_dark", "zu dunkel")]
    [InlineData("too_bright", "zu hell")]
    [InlineData("too_uniform", "ohne Struktur")]
    [InlineData("too_blurry", "unscharf")]
    public async Task Unbrauchbares_bild_mit_freigegebenem_yolo_ist_nie_gruen(string grund, string text)
    {
        var result = await Analysiere(new Handler(qualifiziert: true, frameClass: grund, nutzbar: false, qualitaet: grund));

        Assert.True(result.Degraded);
        Assert.Contains("Bild nicht beurteilbar", result.DegradedReason);
        Assert.Contains(text, result.DegradedReason);
    }

    [Fact]
    public async Task Gesundes_leeres_bild_bleibt_ohne_befund()
    {
        var result = await Analysiere(new Handler(qualifiziert: true, frameClass: "irrelevant", nutzbar: true, qualitaet: "ok"));

        Assert.False(result.Degraded);
        Assert.False(result.IsRelevant);
    }

    [Fact]
    public async Task Heute_ohne_freigegebenes_yolo_nennt_die_meldung_den_wahren_grund()
    {
        var result = await Analysiere(new Handler(qualifiziert: false, frameClass: "irrelevant", nutzbar: false, qualitaet: "too_dark"));

        Assert.True(result.Degraded);
        Assert.Contains("zu dunkel", result.DegradedReason);
    }
}
