using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Sicherungs-Tests fuer das Sidecar-HTTP-Contract. Stubt den HttpClient via
/// CapturingStubHandler und friert das heutige Verhalten von
/// <see cref="VisionPipelineClient"/> ein, bevor die Klasse refaktoriert wird.
/// </summary>
public class SidecarContractTests
{
    private static readonly Uri BaseUri = new("http://localhost:8100");

    [Fact]
    public async Task HealthCheck_Status200WithJson_DeserializesResponse()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, """
                {
                    "status": "ok",
                    "version": "1.2.3",
                    "gpu": null,
                    "yolo": null,
                    "nvdec": null,
                    "vsr": null
                }
                """)
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));

        var result = await client.HealthCheckAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("ok", result!.Status);
        Assert.Equal("1.2.3", result.Version);
        Assert.Null(client.LastHealthError);
    }

    [Fact]
    public async Task HealthCheck_Status401_ReturnsNullAndSetsLastHealthError()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                ReasonPhrase = "Unauthorized"
            }
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));

        var result = await client.HealthCheckAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.NotNull(client.LastHealthError);
        Assert.Contains("401", client.LastHealthError);
    }

    [Fact]
    public async Task HealthCheck_Status500_ReturnsNullAndSetsLastHealthError()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = "Internal Server Error"
            }
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));

        var result = await client.HealthCheckAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.NotNull(client.LastHealthError);
        Assert.Contains("500", client.LastHealthError);
    }

    [Fact]
    public async Task HealthCheck_WithExplicitToken_AddsXSidecarTokenHeader()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, """{"status":"ok","version":"1.0","gpu":null,"yolo":null,"nvdec":null,"vsr":null}""")
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub), authToken: "secret-token-42");

        await client.HealthCheckAsync(CancellationToken.None);

        var captured = Assert.Single(stub.Requests);
        Assert.True(captured.Headers.TryGetValues("X-Sidecar-Token", out var values),
            "X-Sidecar-Token-Header fehlt obwohl Token explizit gesetzt war");
        Assert.Equal("secret-token-42", values!.Single());
    }

    [Fact]
    public async Task HealthCheck_WithoutToken_DoesNotAddXSidecarTokenHeader()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, """{"status":"ok","version":"1.0","gpu":null,"yolo":null,"nvdec":null,"vsr":null}""")
        };
        // authToken explizit null + Resolver in SidecarAuthTokenAccessor nicht gesetzt
        // (Test-Prozess) => kein Token wird hinzugefuegt.
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub), authToken: null);

        await client.HealthCheckAsync(CancellationToken.None);

        var captured = Assert.Single(stub.Requests);
        Assert.False(captured.Headers.Contains("X-Sidecar-Token"),
            "X-Sidecar-Token-Header darf ohne Token nicht gesetzt sein");
    }

    [Fact]
    public async Task HealthCheck_TargetsSlashHealthEndpoint()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, """{"status":"ok","version":"1.0","gpu":null,"yolo":null,"nvdec":null,"vsr":null}""")
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));

        await client.HealthCheckAsync(CancellationToken.None);

        var captured = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.NotNull(captured.RequestUri);
        Assert.Equal("/health", captured.RequestUri!.AbsolutePath);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    /// <summary>
    /// HttpMessageHandler fuer Tests. Speichert alle Requests und liefert die
    /// per Responder konfigurierte Antwort. Wenn kein Responder gesetzt ist,
    /// wird ein leeres 200 OK zurueckgegeben.
    /// </summary>
    private sealed class CapturingStubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var resp = Responder is null
                ? new HttpResponseMessage(HttpStatusCode.OK)
                : Responder(request);
            return Task.FromResult(resp);
        }
    }
}
