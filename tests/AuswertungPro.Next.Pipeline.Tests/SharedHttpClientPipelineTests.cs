using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Durchstich fuer den P1-Fix: In der Videoanalyse teilt der DataPageVideoAnalysisController
/// EINEN HttpClient ueber Laeufe. RunAsync nutzt ihn zuerst fuer den Sidecar-Health-Check
/// (VisionPipelineClient) und danach fuer Ollama (OllamaClient). Vor dem Fix warf das
/// BaseAddress-Setzen im OllamaClient auf dem bereits benutzten Client InvalidOperationException.
/// </summary>
public sealed class SharedHttpClientPipelineTests
{
    [Fact]
    public async Task SidecarHealthThenOllama_AufGeteiltemClient_WirftNicht()
    {
        var handler = new SharedEndpointHandler();
        using var shared = new HttpClient(handler);

        // 1) Sidecar-Health ueber den geteilten Client -> Client gilt danach als "gestartet".
        var sidecar = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), shared, sidecarToken: "t");
        _ = await sidecar.CheckHealthDetailedAsync(CancellationToken.None);

        // 2) Ollama ueber DENSELBEN Client -> darf nicht mit InvalidOperationException scheitern.
        using var ollama = new OllamaClient(new Uri("http://127.0.0.1:11434"), shared);
        var ex = await Record.ExceptionAsync(() =>
            ollama.ChatAsync("m", new[] { new OllamaClient.ChatMessage("user", "ping") }, CancellationToken.None));

        Assert.Null(ex);
        Assert.True(handler.SawHealth, "Health-Endpunkt wurde nicht getroffen.");
        Assert.True(handler.SawChat, "Ollama-Chat-Endpunkt wurde nicht getroffen.");
        // Beide Aufrufe gingen an ihren jeweiligen absoluten Host trotz geteiltem Client.
        Assert.Contains("127.0.0.1:8100", handler.HealthUri);
        Assert.Contains("127.0.0.1:11434", handler.ChatUri);
    }

    private sealed class SharedEndpointHandler : HttpMessageHandler
    {
        public bool SawHealth { get; private set; }
        public bool SawChat { get; private set; }
        public string HealthUri { get; private set; } = "";
        public string ChatUri { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            string json;
            if (uri.Contains("/api/chat", StringComparison.Ordinal))
            {
                SawChat = true;
                ChatUri = uri;
                json = """{"message":{"role":"assistant","content":"ok"}}""";
            }
            else
            {
                SawHealth = true;
                HealthUri = uri;
                json = """{"status":"ok"}""";
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
