using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OllamaClientTests
{
    [Fact]
    public void Constructor_OwnedClient_UsesConfiguredTimeout()
    {
        var uri = new Uri("http://localhost:11434");
        var client = new OllamaClient(uri, ownedTimeout: TimeSpan.FromMinutes(42));

        var http = ExtractHttpClient(client);

        // BaseAddress wird bewusst NICHT mehr gesetzt: Endpunkte werden absolut gebaut,
        // damit ein geteilter (bereits benutzter) HttpClient nicht wirft.
        Assert.Null(http.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(42), http.Timeout);
    }

    [Fact]
    public void Constructor_ProvidedClient_DoesNotTouchSharedClient()
    {
        var uri = new Uri("http://localhost:11434");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        _ = new OllamaClient(uri, http, TimeSpan.FromMinutes(42));

        // Der geteilte Client bleibt unangetastet (keine BaseAddress, kein Timeout-Overwrite).
        Assert.Null(http.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(3), http.Timeout);
    }

    [Fact]
    public void Constructor_SharedClientThatAlreadySent_DoesNotThrow()
    {
        // Bildet den Videoanalyse-Pfad nach: ein geteilter HttpClient hat vor dem Ollama-Aufruf
        // bereits eine Anfrage gesendet (Sidecar-Health-Check). Vor dem Fix warf hier das
        // BaseAddress-Setzen im Konstruktor InvalidOperationException und die Batch-Analyse brach ab.
        var handler = new CaptureOllamaHandler("{\"ok\":true}");
        using var http = new HttpClient(handler);
        using (http.GetAsync("http://127.0.0.1:8000/health").GetAwaiter().GetResult()) { }

        var ex = Record.Exception(() => new OllamaClient(new Uri("http://localhost:11434"), http));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ChatAsync_SharedClientThatAlreadySent_HitsAbsoluteEndpoint()
    {
        var handler = new CaptureOllamaHandler("antwort");
        using var http = new HttpClient(handler);
        // Client zuerst als "gestartet" markieren (wie der vorausgehende Sidecar-Health-Check).
        using (http.GetAsync("http://127.0.0.1:8000/health").GetAwaiter().GetResult()) { }
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http);

        await client.ChatAsync(
            "qwen-test",
            [new OllamaClient.ChatMessage("user", "ping")],
            CancellationToken.None);

        Assert.Equal("http://localhost:11434/api/chat", handler.LastRequestUri);
    }

    [Fact]
    public async Task ChatStructuredWithOptionsAsync_keeps_explicit_num_ctx_over_client_default()
    {
        var handler = new CaptureOllamaHandler("""
            {"ok": true}
            """);
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var client = new OllamaClient(new Uri("http://localhost:11434"), http, numCtx: 8192);

        await client.ChatStructuredWithOptionsAsync<TestDto>(
            "qwen-test",
            [new OllamaClient.ChatMessage("user", "ping")],
            JsonSerializer.Deserialize<JsonElement>("""
                {"type":"object","additionalProperties":false,"properties":{"ok":{"type":"boolean"}},"required":["ok"]}
                """),
            new Dictionary<string, object>
            {
                ["temperature"] = 0,
                ["seed"] = 42,
                ["num_ctx"] = 12288
            },
            CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.LastRequestJson);
        var options = doc.RootElement.GetProperty("options");
        Assert.Equal(0, options.GetProperty("temperature").GetInt32());
        Assert.Equal(42, options.GetProperty("seed").GetInt32());
        Assert.Equal(12288, options.GetProperty("num_ctx").GetInt32());
    }

    private static HttpClient ExtractHttpClient(OllamaClient client)
    {
        var field = typeof(OllamaClient).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<HttpClient>(field!.GetValue(client));
    }

    private sealed record TestDto(bool Ok);

    private sealed class CaptureOllamaHandler(string structuredContent) : HttpMessageHandler
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

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
