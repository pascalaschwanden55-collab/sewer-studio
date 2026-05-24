using System.Net;
using System.Net.Http;
using System.Text;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EnhancedVisionAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_maps_snake_case_structured_json_fields()
    {
        var content = """
            {
              "meter": 12.5,
              "time_in_video": 44.0,
              "pipe_material": "beton",
              "pipe_diameter_mm": 300,
              "findings": [
                {
                  "label": "Wasserstand",
                  "vsa_code_hint": "BDDC",
                  "severity": 3,
                  "position_clock": "6:00",
                  "extent_percent": 40,
                  "height_mm": 12,
                  "width_mm": 220,
                  "intrusion_percent": null,
                  "cross_section_reduction_percent": 15,
                  "diameter_reduction_mm": null,
                  "bbox": [0.1, 0.2, 0.7, 0.8],
                  "notes": "sichtbar"
                }
              ],
              "image_quality": "gut",
              "is_empty_frame": false
            }
            """;
        using var http = new HttpClient(new StaticOllamaHandler(content))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = new EnhancedVisionAnalysisService(client, "qwen-test");

        var result = await service.AnalyzeAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.Null(result.Error);
        Assert.Equal(12.5, result.Meter);
        Assert.Equal("beton", result.PipeMaterial);
        Assert.Equal(300, result.PipeDiameterMm);
        Assert.False(result.IsEmptyFrame);
        Assert.Single(result.Findings);
        Assert.Equal("BDDC", result.Findings[0].VsaCodeHint);
        Assert.Equal("6:00", result.Findings[0].PositionClock);
        Assert.Equal(15, result.Findings[0].CrossSectionReductionPercent);
        Assert.Equal(0.7, result.Findings[0].BboxX2);
    }

    private sealed class StaticOllamaHandler(string structuredContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var responseJson = $$"""
                {
                  "message": {
                    "role": "assistant",
                    "content": {{System.Text.Json.JsonSerializer.Serialize(structuredContent)}}
                  }
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
