using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EnhancedVisionAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_with_import_context_prompt_discourages_empty_frame_for_known_findings()
    {
        var content = """
            {
              "meter": null,
              "time_in_video": null,
              "pipe_material": "unbekannt",
              "pipe_diameter_mm": null,
              "findings": [],
              "image_quality": "mittel",
              "is_empty_frame": true
            }
            """;
        using var http = new HttpClient(new StaticOllamaHandler(content))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = new EnhancedVisionAnalysisService(client, "qwen-test");

        await service.AnalyzeAsync(
            Convert.ToBase64String([1, 2, 3]),
            [("BDDC", "Wasserstand sichtbar", 12.3)]);

        Assert.Contains("BEKANNTE BEFUNDE", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("is_empty_frame=true nur dann setzen", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("bekannten Befunde sichtbar", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("Wasserstand", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("BDDC", StaticOllamaHandler.LastRequestJson);
    }

    [Fact]
    public async Task AnalyzeWithObservationHintsAsync_adds_non_binding_yolo_hint_to_prompt()
    {
        var content = """
            {
              "meter": null,
              "time_in_video": null,
              "pipe_material": "unbekannt",
              "pipe_diameter_mm": null,
              "findings": [],
              "image_quality": "mittel",
              "is_empty_frame": true
            }
            """;
        using var http = new HttpClient(new StaticOllamaHandler(content))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = new EnhancedVisionAnalysisService(client, "qwen-test");

        await service.AnalyzeWithObservationHintsAsync(
            Convert.ToBase64String([1, 2, 3]),
            ["YOLO sieht eventuell riss_bruch (72 %)"]);

        Assert.Contains("ZUSAETZLICHE BILD-HINWEISE", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("riss_bruch", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("nicht als VSA-Code", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("is_empty_frame=true nur dann setzen", StaticOllamaHandler.LastRequestJson);
    }

    [Fact]
    public async Task AnalyzeAsync_uses_deterministic_ollama_options()
    {
        var content = """
            {
              "meter": null,
              "time_in_video": null,
              "pipe_material": "unbekannt",
              "pipe_diameter_mm": null,
              "findings": [],
              "image_quality": "mittel",
              "is_empty_frame": true
            }
            """;
        using var http = new HttpClient(new StaticOllamaHandler(content))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = new EnhancedVisionAnalysisService(client, "qwen-test");

        await service.AnalyzeAsync(Convert.ToBase64String([1, 2, 3]));

        using var doc = JsonDocument.Parse(StaticOllamaHandler.LastRequestJson);
        var options = doc.RootElement.GetProperty("options");
        Assert.Equal(0, options.GetProperty("temperature").GetInt32());
        Assert.Equal(42, options.GetProperty("seed").GetInt32());
        Assert.Equal(12288, options.GetProperty("num_ctx").GetInt32());
    }

    [Fact]
    public async Task AnalyzeAsync_renders_damage_classes_from_vsa_catalog()
    {
        var content = """
            {
              "meter": null,
              "time_in_video": null,
              "pipe_material": "unbekannt",
              "pipe_diameter_mm": null,
              "findings": [],
              "image_quality": "mittel",
              "is_empty_frame": true
            }
            """;
        using var http = new HttpClient(new StaticOllamaHandler(content))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = new EnhancedVisionAnalysisService(
            client,
            "qwen-test",
            VsaResolverTestCatalog.CreateDefault());

        await service.AnalyzeAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.Contains("BBA = Wurzeln", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("BBB = Anhaftende Stoffe", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("BAA = Verformung", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("BAF = Oberflaechenschaden", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("BAJ = Verschobene Rohrverbindung", StaticOllamaHandler.LastRequestJson);
        Assert.DoesNotContain("BBB = Bewuchs/Wurzeln", StaticOllamaHandler.LastRequestJson);
        Assert.DoesNotContain("BBA = Inkrustation", StaticOllamaHandler.LastRequestJson);
        Assert.DoesNotContain("BAF = Deformation", StaticOllamaHandler.LastRequestJson);
        Assert.DoesNotContain("BAJ = Ausbrueche/Abplatzungen", StaticOllamaHandler.LastRequestJson);
    }

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
        public static string LastRequestJson { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestJson = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";

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
