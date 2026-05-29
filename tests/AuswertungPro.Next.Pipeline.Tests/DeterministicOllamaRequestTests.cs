using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class DeterministicOllamaRequestTests
{
    [Fact]
    public async Task OllamaProtocolAiService_uses_deterministic_options_for_text_mapping()
    {
        using var http = new HttpClient(new CaptureOllamaHandler(AiSuggestionJson()))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var service = new OllamaProtocolAiService(
            enabled: true,
            config: new(
                new Uri("http://localhost:11434"),
                "qwen-vision",
                "qwen-text",
                "nomic-embed-text",
                TimeSpan.FromSeconds(30),
                NumCtx: 8192),
            ffmpegPath: null,
            http: http);

        await service.SuggestAsync(new AiInput(
            ProjectFolderAbs: Environment.CurrentDirectory,
            HaltungId: "H-1",
            Meter: 1.2,
            ExistingCode: null,
            ExistingText: "Riss sichtbar",
            AllowedCodes: ["BAA"]));

        AssertDeterministicOptions(CaptureOllamaHandler.LastRequestJson);
    }

    [Fact]
    public async Task FullProtocolGenerationService_uses_deterministic_options_for_text_mapping()
    {
        using var http = new HttpClient(new CaptureOllamaHandler(AiSuggestionJson()))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var service = new FullProtocolGenerationService(
            new AiRuntimeSettings(
                Enabled: true,
                OllamaBaseUri: new Uri("http://localhost:11434"),
                VisionModel: "qwen-vision",
                TextModel: "qwen-text",
                EmbedModel: "nomic-embed-text",
                FfmpegPath: null,
                OllamaRequestTimeout: TimeSpan.FromSeconds(30),
                OllamaKeepAlive: "24h",
                OllamaNumCtx: 8192),
            new RuleBasedAiSuggestionPlausibilityService(new HashSet<string>(["BAA"])),
            http,
            retrieval: new EmptyRetrievalService());

        await service.GenerateFromDetectionsAsync(
            [new RawVideoDetection("Riss", 1.0, 1.2, "mid", "BAA")],
            new FullProtocolGenerationRequest(
                HaltungId: "H-1",
                VideoPath: "video.mp4",
                AllowedCodes: ["BAA"]));

        AssertDeterministicOptions(CaptureOllamaHandler.LastRequestJson);
    }

    private static string AiSuggestionJson() => """
        {
          "suggestedCode": "BAA",
          "confidence": 0.9,
          "rationale": "test",
          "evidence": "test",
          "warnings": []
        }
        """;

    private static void AssertDeterministicOptions(string requestJson)
    {
        using var doc = JsonDocument.Parse(requestJson);
        var options = doc.RootElement.GetProperty("options");
        Assert.Equal(0, options.GetProperty("temperature").GetInt32());
        Assert.Equal(42, options.GetProperty("seed").GetInt32());
        Assert.Equal(12288, options.GetProperty("num_ctx").GetInt32());
    }

    private sealed class CaptureOllamaHandler(string structuredContent) : HttpMessageHandler
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
                    "content": {{JsonSerializer.Serialize(structuredContent)}}
                  }
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class EmptyRetrievalService : IRetrievalService
    {
        public string? StoredEmbedModel => null;
        public bool HasModelMismatch => false;

        public Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
            string queryText,
            int topK = 5,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RetrievalResult>>(Array.Empty<RetrievalResult>());

        public bool CheckModelConsistency() => true;
    }
}
