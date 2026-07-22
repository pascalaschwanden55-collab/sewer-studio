using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class BcaFineCodeClassifierTests
{
    private static (OllamaClient client, HttpClient http) FakeQwen(string structuredContent)
    {
        var http = new HttpClient(new StaticHandler(structuredContent))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        return (new OllamaClient(new Uri("http://localhost:11434"), http), http);
    }

    [Fact]
    public async Task Liefert_Bauart_Kandidat_aus_Qwen_Antwort()
    {
        var (client, http) = FakeQwen("""
            { "code": "BCAAA", "confidence": 0.8, "is_uncertain": false }
            """);
        using var _ = http;
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        var result = await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.False(result.IsUncertain);
        Assert.Single(result.Candidates);
        Assert.Equal("BCAAA", result.Candidates[0].VsaCode);
        Assert.Equal(0.8, result.Candidates[0].Confidence);
    }

    [Fact]
    public async Task Unsicheres_Qwen_Ergebnis_liefert_leere_Kandidaten()
    {
        var (client, http) = FakeQwen("""
            { "code": "unsicher", "confidence": 0.0, "is_uncertain": true }
            """);
        using var _ = http;
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        var result = await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.True(result.IsUncertain);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task Unbekannter_Code_wird_als_unsicher_behandelt()
    {
        // Qwen liefert einen Code ausserhalb der 16 gueltigen BCA-Feincodes.
        var (client, http) = FakeQwen("""
            { "code": "BABBA", "confidence": 0.9, "is_uncertain": false }
            """);
        using var _ = http;
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        var result = await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.True(result.IsUncertain);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task Transportfehler_liefert_leere_Kandidaten_ohne_Wurf()
    {
        var http = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var _ = http;
        var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        var result = await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.True(result.IsUncertain);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task Prompt_enthaelt_die_gueltigen_Bauart_Codes()
    {
        var (client, http) = FakeQwen("""
            { "code": "unsicher", "confidence": 0.0, "is_uncertain": true }
            """);
        using var _ = http;
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.Contains("BCAAA", StaticHandler.LastRequestJson);
        Assert.Contains("BCAEA", StaticHandler.LastRequestJson);
    }

    private sealed class StaticHandler(string structuredContent) : HttpMessageHandler
    {
        public static string LastRequestJson { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestJson = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";
            var responseJson = $$"""
                { "message": { "role": "assistant",
                  "content": {{System.Text.Json.JsonSerializer.Serialize(structuredContent)}} } }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }
}
