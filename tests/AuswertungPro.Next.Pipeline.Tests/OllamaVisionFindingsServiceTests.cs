using System.Net;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OllamaVisionFindingsServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_nutzt_striktes_JsonSchema_mit_Bild()
    {
        var handler = new OllamaResponseHandler("""
            {"meter":18.4,"findings":["Riss"],"severity":"mid"}
            """);
        using var http = new HttpClient(handler);
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = new OllamaVisionFindingsService(client, "qwen-test");

        var result = await service.AnalyzeAsync("bild-base64", CancellationToken.None);

        Assert.Equal(18.4, result.Meter);
        Assert.Equal(["Riss"], result.Findings);
        Assert.Equal("mid", result.Severity);
        Assert.Equal("http://localhost:11434/api/chat", handler.LastRequestUri);

        using var request = JsonDocument.Parse(handler.LastRequestJson);
        var root = request.RootElement;
        var schema = root.GetProperty("format");
        Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);
        Assert.Collection(
            schema.GetProperty("required").EnumerateArray(),
            item => Assert.Equal("meter", item.GetString()),
            item => Assert.Equal("findings", item.GetString()),
            item => Assert.Equal("severity", item.GetString()));
        Assert.Collection(
            schema.GetProperty("properties").GetProperty("severity").GetProperty("enum").EnumerateArray(),
            item => Assert.Equal("low", item.GetString()),
            item => Assert.Equal("mid", item.GetString()),
            item => Assert.Equal("high", item.GetString()));
        Assert.Equal(
            "bild-base64",
            root.GetProperty("messages")[0].GetProperty("images")[0].GetString());
        Assert.Equal(0, root.GetProperty("options").GetProperty("temperature").GetInt32());
    }

    [Fact]
    public async Task AnalyzeAsync_unvollstaendige_Antwort_faellt_geschlossen_auf_leer_zurueck()
    {
        var handler = new OllamaResponseHandler("""
            {"meter":18.4,"findings":["Riss"]}
            """);
        using var http = new HttpClient(handler);
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = new OllamaVisionFindingsService(client, "qwen-test");

        var result = await service.AnalyzeAsync("bild-base64", CancellationToken.None);

        Assert.Null(result.Meter);
        Assert.Empty(result.Findings);
        Assert.Equal("low", result.Severity);
    }

    private sealed class OllamaResponseHandler(string structuredContent) : HttpMessageHandler
    {
        public string LastRequestJson { get; private set; } = "";
        public string LastRequestUri { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString() ?? "";
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
}
