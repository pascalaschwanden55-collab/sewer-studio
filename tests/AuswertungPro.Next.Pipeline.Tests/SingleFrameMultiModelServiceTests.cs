using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SingleFrameMultiModelServiceTests
{
    [Theory]
    [InlineData("BCD", 0.2, 50.0)]
    [InlineData("BCE", 49.2, 50.0)]
    public async Task AnalyzeFrameAsync_returns_boundary_code_from_classifier_even_without_yolo_detection(
        string boundaryCode,
        double currentMeter,
        double reachLength)
    {
        var handler = new RouteHandler(boundaryCode);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler),
            sidecarToken: "test-token");
        var service = new SingleFrameMultiModelService(client);

        var result = await service.AnalyzeFrameAsync(
            [1, 2, 3],
            pipeDiameterMm: 300,
            calibration: null,
            currentMeterM: currentMeter,
            reachLengthM: reachLength);

        Assert.True(result.IsRelevant);
        Assert.Equal(boundaryCode, result.ClassifierCode);
        Assert.False(result.HasDetections);
        Assert.Contains("/classify/yolo", handler.Paths);
    }

    [Fact]
    public async Task AnalyzeFrameAsync_returns_rohrende_from_end_zone_when_classifier_is_uncertain()
    {
        var handler = new StaticClassifierHandler("""
        {
            "predictions": [
                { "class_name": "LEER", "confidence": 0.51 },
                { "class_name": "BDA", "confidence": 0.37 },
                { "class_name": "BCE", "confidence": 0.03 }
            ],
            "inference_time_ms": 12,
            "usable": true,
            "quality_reason": "ok",
            "model_name": "vsa_cls_v5_nocrop",
            "model_source": "active.json",
            "classifier_loaded": true
        }
        """);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler),
            sidecarToken: "test-token");
        var service = new SingleFrameMultiModelService(client);

        var result = await service.AnalyzeFrameAsync(
            [1, 2, 3],
            pipeDiameterMm: 300,
            calibration: null,
            currentMeterM: 49.7,
            reachLengthM: 50.0);

        Assert.True(result.IsRelevant);
        Assert.Equal("BCE", result.ClassifierCode);
        Assert.False(result.HasDetections);
    }

    [Fact]
    public async Task AnalyzeFrameAsync_bend_veto_prevents_rohrende_in_end_zone()
    {
        // Identische Endzonen-Situation wie der Rohrende-Test, ABER der Sidecar meldet
        // per Geometrie einen Bogen (is_bend=true). Dann darf NICHT BCE Rohrende gesetzt
        // werden - der Bogen wird sonst als Rohrende verkannt (User-Fall 1077586-1077458).
        var handler = new StaticClassifierHandler("""
        {
            "predictions": [
                { "class_name": "LEER", "confidence": 0.51 },
                { "class_name": "BDA", "confidence": 0.37 },
                { "class_name": "BCE", "confidence": 0.03 }
            ],
            "inference_time_ms": 12,
            "usable": true,
            "quality_reason": "ok",
            "model_name": "vsa_cls_v5_nocrop",
            "model_source": "active.json",
            "classifier_loaded": true,
            "is_bend": true,
            "bend_shift": 0.13
        }
        """);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler),
            sidecarToken: "test-token");
        var service = new SingleFrameMultiModelService(client);

        var result = await service.AnalyzeFrameAsync(
            [1, 2, 3],
            pipeDiameterMm: 300,
            calibration: null,
            currentMeterM: 49.7,
            reachLengthM: 50.0);

        // Kein positionsgetriebenes BCE mehr - der Bogen kippt die Endzonen-Regel.
        Assert.NotEqual("BCE", result.ClassifierCode);
    }

    [Fact]
    public async Task AnalyzeFrameAsync_bend_veto_prevents_top1_bce_rohrende()
    {
        var handler = new StaticClassifierHandler("""
        {
            "predictions": [
                { "class_name": "BCE", "confidence": 0.91 },
                { "class_name": "LEER", "confidence": 0.03 }
            ],
            "inference_time_ms": 12,
            "usable": true,
            "quality_reason": "ok",
            "model_name": "vsa_cls_v5_nocrop",
            "model_source": "active.json",
            "classifier_loaded": true,
            "is_bend": true,
            "bend_shift": 0.18
        }
        """);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler),
            sidecarToken: "test-token");
        var service = new SingleFrameMultiModelService(client);

        var result = await service.AnalyzeFrameAsync(
            [1, 2, 3],
            pipeDiameterMm: 300,
            calibration: null,
            currentMeterM: 49.7,
            reachLengthM: 50.0);

        Assert.True(result.IsRelevant);
        Assert.Equal("BCC", result.ClassifierCode);
        Assert.False(result.HasDetections);
    }

    [Fact]
    public async Task AnalyzeFrameAsync_bend_veto_failure_does_not_trust_false_is_bend()
    {
        var handler = new StaticClassifierHandler("""
        {
            "predictions": [
                { "class_name": "BCE", "confidence": 0.91 },
                { "class_name": "LEER", "confidence": 0.03 }
            ],
            "inference_time_ms": 12,
            "usable": true,
            "quality_reason": "ok",
            "model_name": "vsa_cls_v5_nocrop",
            "model_source": "active.json",
            "classifier_loaded": true,
            "is_bend": false,
            "bend_veto_failed": true,
            "bend_shift": 0.0
        }
        """);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler),
            sidecarToken: "test-token");
        var service = new SingleFrameMultiModelService(client);

        var result = await service.AnalyzeFrameAsync(
            [1, 2, 3],
            pipeDiameterMm: 300,
            calibration: null,
            currentMeterM: 49.7,
            reachLengthM: 50.0);

        Assert.False(result.IsRelevant);
        Assert.Null(result.ClassifierCode);
        Assert.False(result.HasDetections);
    }

    [Fact]
    public async Task AnalyzeFrameAsync_does_not_replace_clear_defect_code_with_rohrende()
    {
        var handler = new StaticClassifierHandler("""
        {
            "predictions": [
                { "class_name": "BDA", "confidence": 0.91 },
                { "class_name": "BCE", "confidence": 0.03 }
            ],
            "inference_time_ms": 12,
            "usable": true,
            "quality_reason": "ok",
            "model_name": "vsa_cls_v5_nocrop",
            "model_source": "active.json",
            "classifier_loaded": true
        }
        """);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler),
            sidecarToken: "test-token");
        var service = new SingleFrameMultiModelService(client);

        var result = await service.AnalyzeFrameAsync(
            [1, 2, 3],
            pipeDiameterMm: 300,
            calibration: null,
            currentMeterM: 49.7,
            reachLengthM: 50.0);

        Assert.False(result.IsRelevant);
        Assert.Equal("BDA", result.ClassifierCode);
    }

    [Theory]
    [InlineData("BCA")]
    [InlineData("BCC")]
    public async Task AnalyzeFrameAsync_keeps_structural_classifier_code_when_yolo_is_irrelevant(
        string structuralCode)
    {
        var handler = new StaticClassifierHandler($$"""
        {
            "predictions": [
                { "class_name": "{{structuralCode}}", "confidence": 0.91 },
                { "class_name": "LEER", "confidence": 0.03 }
            ],
            "inference_time_ms": 12,
            "usable": true,
            "quality_reason": "ok",
            "model_name": "vsa_cls_v5_nocrop",
            "model_source": "active.json",
            "classifier_loaded": true
        }
        """);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler),
            sidecarToken: "test-token");
        var service = new SingleFrameMultiModelService(client);

        var result = await service.AnalyzeFrameAsync(
            [1, 2, 3],
            pipeDiameterMm: 300,
            calibration: null,
            currentMeterM: 2.0,
            reachLengthM: 10.0);

        Assert.True(result.IsRelevant);
        Assert.Equal(structuralCode, result.ClassifierCode);
        Assert.False(result.HasDetections);
    }

    [Fact]
    public async Task AnalyzeFrameAsync_keeps_clear_mid_pipe_rohrende_candidate()
    {
        var handler = new StaticClassifierHandler("""
        {
            "predictions": [
                { "class_name": "BCE", "confidence": 0.91 },
                { "class_name": "LEER", "confidence": 0.03 }
            ],
            "inference_time_ms": 12,
            "usable": true,
            "quality_reason": "ok",
            "model_name": "vsa_cls_v5_nocrop",
            "model_source": "active.json",
            "classifier_loaded": true
        }
        """);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler),
            sidecarToken: "test-token");
        var service = new SingleFrameMultiModelService(client);

        var result = await service.AnalyzeFrameAsync(
            [1, 2, 3],
            pipeDiameterMm: 300,
            calibration: null,
            currentMeterM: 0.71,
            reachLengthM: 10.0);

        Assert.True(result.IsRelevant);
        Assert.Equal("BCE", result.ClassifierCode);
        Assert.False(result.HasDetections);
    }

    private sealed class RouteHandler(string boundaryCode) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            Paths.Add(path);

            var json = path switch
            {
                "/classify/yolo" => $$"""
                {
                    "predictions": [
                        { "class_name": "{{boundaryCode}}", "confidence": 0.91 },
                        { "class_name": "LEER", "confidence": 0.03 }
                    ],
                    "inference_time_ms": 12,
                    "usable": true,
                    "quality_reason": "ok",
                    "model_name": "vsa_cls_v5_nocrop",
                    "model_source": "active.json",
                    "classifier_loaded": true
                }
                """,
                "/detect/yolo" => """
                {
                    "is_relevant": false,
                    "detections": [],
                    "frame_class": "irrelevant",
                    "inference_time_ms": 4
                }
                """,
                "/detect/dino" => """
                {
                    "detections": [],
                    "inference_time_ms": 7
                }
                """,
                _ => throw new InvalidOperationException($"Unexpected endpoint: {path}")
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StaticClassifierHandler(string classifierJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var json = path switch
            {
                "/classify/yolo" => classifierJson,
                "/detect/yolo" => """
                {
                    "is_relevant": false,
                    "detections": [],
                    "frame_class": "irrelevant",
                    "inference_time_ms": 4
                }
                """,
                "/detect/dino" => """
                {
                    "detections": [],
                    "inference_time_ms": 7
                }
                """,
                _ => throw new InvalidOperationException($"Unexpected endpoint: {path}")
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task DinoDegraded_wird_als_degraded_gemeldet_nicht_als_kein_schaden()
    {
        // U1: Ein DINO-Modellfehler (degraded=true, leere Detektionen) darf im Codiermodus NICHT
        // als gruenes "kein Schaden" erscheinen, sondern muss als degraded durchgereicht werden.
        var service = new SingleFrameMultiModelService(new DinoDegradedClient());

        var result = await service.AnalyzeFrameAsync(
            new byte[] { 1, 2, 3 }, pipeDiameterMm: 300, calibration: null,
            currentMeterM: 25.0, reachLengthM: 50.0);

        Assert.True(result.IsRelevant);
        Assert.False(result.HasDetections);
        Assert.True(result.Degraded, "DINO-Modellfehler muss als degraded markiert sein.");
        Assert.Contains("DINO", result.DegradedReason ?? "");
    }

    private sealed class DinoDegradedClient : IVisionPipelineClient
    {
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(null);

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloResponse(
                IsRelevant: true, Detections: Array.Empty<YoloDetectionDto>(),
                FrameClass: "structural", InferenceTimeMs: 1));

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
            => Task.FromResult(new DinoResponse(Array.Empty<DinoDetectionDto>(), 1, Degraded: true, Error: "boom"));

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => Task.FromResult(new SamResponse(Array.Empty<SamMaskResult>(), 640, 480, 1));

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloClassifyResponse(
                Array.Empty<YoloClassifyPrediction>(), 1, Usable: true, QualityReason: "ok", ClassifierLoaded: true));
    }

}
