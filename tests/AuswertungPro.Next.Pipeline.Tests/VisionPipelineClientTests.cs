using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Pipeline;

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
            "inference_time_ms": 42.5
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
}
