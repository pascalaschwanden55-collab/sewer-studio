using System.Net;
using System.Text;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class LiveDetectionServiceTests
{
    [Fact]
    public async Task AnalyzeFrameAsync_prompt_grenzt_truebes_abwasser_gegen_normalfluss_ab()
    {
        using var http = new HttpClient(new StaticOllamaHandler())
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = new LiveDetectionService(client, "qwen-test");

        await service.AnalyzeFrameAsync([1, 2, 3], 0, CancellationToken.None);

        Assert.Contains("Normal fliessendes oder nur truebes Abwasser ist KEIN Schaden", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("Wasserstand/BDDC nur melden", StaticOllamaHandler.LastRequestJson);
        Assert.Contains("Rueckstau", StaticOllamaHandler.LastRequestJson);
    }

    private sealed class StaticOllamaHandler : HttpMessageHandler
    {
        public static string LastRequestJson { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestJson = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";

            const string content = "{\"meter\": null, \"findings\": []}";
            var responseJson = $$"""
                {
                  "message": {
                    "role": "assistant",
                    "content": {{System.Text.Json.JsonSerializer.Serialize(content)}}
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
