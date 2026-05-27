using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public class VisionPipelineClientTests
{
    [Fact]
    public async Task HealthCheckAsync_UnreachableHost_ReturnsNull()
    {
        // Use a port that is almost certainly not listening
        var client = new VisionPipelineClient(new Uri("http://127.0.0.1:19999"));

        var result = await client.HealthCheckAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void Constructor_SetsBaseAddress()
    {
        var uri = new Uri("http://localhost:8100");
        var httpClient = new HttpClient();
        var client = new VisionPipelineClient(uri, httpClient);

        Assert.Equal(uri, httpClient.BaseAddress);
    }

    [Fact]
    public async Task ClassifyYoloAsync_AddsTokenHeader_ForLoopbackUrl()
    {
        var handler = new CaptureHandler("""{"predictions":[],"inference_time_ms":1}""");
        var httpClient = new HttpClient(handler);
        var client = new VisionPipelineClient(
            new Uri("http://localhost:8100"),
            httpClient,
            sidecarToken: "test-token");

        await client.ClassifyYoloAsync(new YoloClassifyRequest("abc", 1));

        Assert.Equal("test-token", handler.LastSidecarToken);
    }

    [Fact]
    public async Task ClassifyYoloAsync_DoesNotSendToken_ToExternalUrl()
    {
        var handler = new CaptureHandler("""{"predictions":[],"inference_time_ms":1}""");
        var httpClient = new HttpClient(handler);
        var client = new VisionPipelineClient(
            new Uri("http://example.com"),
            httpClient,
            sidecarToken: "test-token");

        await client.ClassifyYoloAsync(new YoloClassifyRequest("abc", 1));

        Assert.Null(handler.LastSidecarToken);
    }

    [Fact]
    public async Task HealthCheckAsync_AddsTokenHeader_ForLoopbackUrl()
    {
        var handler = new CaptureHandler("""{"status":"ok","version":"1.1.0","gpu":null}""");
        var httpClient = new HttpClient(handler);
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            httpClient,
            sidecarToken: "health-token");

        var result = await client.HealthCheckAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("health-token", handler.LastSidecarToken);
    }

    [Fact]
    public void YoloRequest_SerializesCorrectly()
    {
        var request = new YoloRequest("base64data==", 0.3);

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        });

        Assert.Contains("\"image_base64\"", json);
        Assert.Contains("\"confidence_threshold\"", json);
        Assert.Contains("base64data==", json);
        Assert.Contains("0.3", json);
    }

    [Fact]
    public void DinoRequest_SerializesCorrectly()
    {
        var request = new DinoRequest("base64data==", "crack . deposit", 0.3, 0.25);

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        });

        Assert.Contains("\"image_base64\"", json);
        Assert.Contains("\"text_prompt\"", json);
        Assert.Contains("\"box_threshold\"", json);
        Assert.Contains("\"text_threshold\"", json);
    }

    [Fact]
    public void SamRequest_SerializesCorrectly()
    {
        var boxes = new[] { new SamBoundingBox(10, 20, 100, 200, "crack", 0.95) };
        var request = new SamRequest("base64data==", boxes, 300);

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        });

        Assert.Contains("\"image_base64\"", json);
        Assert.Contains("\"bounding_boxes\"", json);
        Assert.Contains("\"pipe_diameter_mm\"", json);
    }

    [Fact]
    public void YoloResponse_DeserializesCorrectly()
    {
        var json = """
        {
            "is_relevant": true,
            "detections": [
                { "x1": 10, "y1": 20, "x2": 100, "y2": 200, "class_name": "crack", "confidence": 0.95 }
            ],
            "frame_class": "relevant",
            "inference_time_ms": 42.5,
            "model_name": "yolo26m.pt",
            "device": "cuda:0",
            "queue_wait_ms": 3.5,
            "vram_allocated_gb": 2.25,
            "vram_total_gb": 31.5
        }
        """;

        var result = JsonSerializer.Deserialize<YoloResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.NotNull(result);
        Assert.True(result.IsRelevant);
        Assert.Single(result.Detections);
        Assert.Equal("crack", result.Detections[0].ClassName);
        Assert.Equal(0.95, result.Detections[0].Confidence);
        Assert.Equal(42.5, result.InferenceTimeMs);
        Assert.Equal("yolo26m.pt", result.ModelName);
        Assert.Equal("cuda:0", result.Device);
        Assert.Equal(3.5, result.QueueWaitMs);
        Assert.Equal(2.25, result.VramAllocatedGb);
        Assert.Equal(31.5, result.VramTotalGb);
    }

    [Fact]
    public async Task DetectYoloAsync_WritesSidecarTelemetryJsonl()
    {
        var previous = Environment.GetEnvironmentVariable("SEWERSTUDIO_TELEMETRY_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "sewer-telemetry-tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("SEWERSTUDIO_TELEMETRY_DIR", tempRoot);

        try
        {
            var handler = new CaptureHandler("""
            {
                "is_relevant": true,
                "detections": [
                    { "x1": 1, "y1": 2, "x2": 3, "y2": 4, "class_name": "roots", "confidence": 0.9 }
                ],
                "frame_class": "relevant",
                "inference_time_ms": 88.5,
                "model_name": "yolo26m.pt",
                "device": "cpu",
                "queue_wait_ms": 0,
                "vram_allocated_gb": 0,
                "vram_total_gb": 31.5
            }
            """);
            var client = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), new HttpClient(handler));

            await client.DetectYoloAsync(new YoloRequest("abc", 0.25));

            var path = SidecarTelemetryWriter.ResolvePath();
            Assert.NotNull(path);
            Assert.True(File.Exists(path), $"Telemetry file missing: {path}");

            var line = File.ReadLines(path).Single();
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            Assert.Equal("/detect/yolo", root.GetProperty("endpoint").GetString());
            Assert.Equal("yolo26m.pt", root.GetProperty("model_name").GetString());
            Assert.Equal("cpu", root.GetProperty("device").GetString());
            Assert.Equal(1, root.GetProperty("detection_count").GetInt32());
            Assert.Equal(88.5, root.GetProperty("inference_time_ms").GetDouble());
            Assert.Equal(31.5, root.GetProperty("vram_total_gb").GetDouble());
            Assert.True(root.GetProperty("roundtrip_ms").GetInt64() >= 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_TELEMETRY_DIR", previous);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MultiModelFrameResult_CanBeConstructed()
    {
        var result = new MultiModelFrameResult(
            TimestampSec: 5.0,
            Meter: 10.5,
            IsRelevant: true,
            DinoDetections: [],
            SamMasks: [],
            ImageWidth: 640,
            ImageHeight: 480,
            YoloTimeMs: 30,
            DinoTimeMs: 150,
            SamTimeMs: 200);

        Assert.Equal(5.0, result.TimestampSec);
        Assert.Equal(10.5, result.Meter);
        Assert.True(result.IsRelevant);
        Assert.Equal(640, result.ImageWidth);
    }

    private sealed class CaptureHandler(string json) : HttpMessageHandler
    {
        public string? LastSidecarToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastSidecarToken = request.Headers.TryGetValues("X-Sidecar-Token", out var values)
                ? values.SingleOrDefault()
                : null;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
