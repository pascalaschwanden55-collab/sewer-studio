using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Pipeline;
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

    // ── Cluster B: POST-Endpoints (DetectYolo / SegmentSam) ─────────────────

    [Fact]
    public async Task DetectYolo_PostsToSlashDetectYolo()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => Json(HttpStatusCode.OK,
                """{"is_relevant":false,"detections":[],"frame_class":"irrelevant","inference_time_ms":1.0}""")
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));

        await client.DetectYoloAsync(new YoloRequest("base64data==", 0.3), CancellationToken.None);

        var captured = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("/detect/yolo", captured.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SegmentSam_PostsToSlashSegmentSam()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => Json(HttpStatusCode.OK,
                """{"masks":[],"inference_time_ms":1.0}""")
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));

        var boxes = new[] { new SamBoundingBox(10, 20, 100, 200, "crack", 0.95) };
        await client.SegmentSamAsync(new SamRequest("base64data==", boxes, PipeDiameterMm: 300), CancellationToken.None);

        var captured = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("/segment/sam", captured.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DetectYolo_BodyContainsImageBase64AndConfidenceThreshold()
    {
        string? capturedBody = null;
        var stub = new CapturingStubHandler
        {
            Responder = req =>
            {
                capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json(HttpStatusCode.OK,
                    """{"is_relevant":false,"detections":[],"frame_class":"irrelevant","inference_time_ms":1.0}""");
            }
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));

        await client.DetectYoloAsync(new YoloRequest("base64data==", 0.42), CancellationToken.None);

        Assert.NotNull(capturedBody);
        Assert.Contains("\"image_base64\"", capturedBody);
        Assert.Contains("base64data==", capturedBody);
        Assert.Contains("\"confidence_threshold\"", capturedBody);
        Assert.Contains("0.42", capturedBody);
    }

    [Fact]
    public async Task DetectYolo_WithToken_AddsXSidecarTokenHeaderOnPost()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => Json(HttpStatusCode.OK,
                """{"is_relevant":false,"detections":[],"frame_class":"irrelevant","inference_time_ms":1.0}""")
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub), authToken: "post-token-99");

        await client.DetectYoloAsync(new YoloRequest("base64data==", 0.3), CancellationToken.None);

        var captured = Assert.Single(stub.Requests);
        Assert.True(captured.Headers.TryGetValues("X-Sidecar-Token", out var values),
            "X-Sidecar-Token-Header fehlt obwohl Token explizit gesetzt war");
        Assert.Equal("post-token-99", values!.Single());
    }

    [Fact]
    public async Task DetectYolo_NonSuccessStatus_ThrowsHttpRequestExceptionWithStatusAndBody()
    {
        var stub = new CapturingStubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    "{\"detail\":\"image_base64 missing\"}",
                    Encoding.UTF8, "application/json")
            }
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.DetectYoloAsync(new YoloRequest("", 0.3), CancellationToken.None));

        // Format aus VisionPipelineClient.PostAsync:
        //   "Sidecar {endpoint} returned {(int)status}: {body}"
        Assert.Contains("/detect/yolo", ex.Message);
        Assert.Contains("400", ex.Message);
        Assert.Contains("image_base64 missing", ex.Message);
    }

    [Fact]
    public async Task DetectYolo_CancelledToken_ThrowsOperationCanceled()
    {
        var stub = new CapturingStubHandler
        {
            // Falls der Stub doch erreicht wird, normal antworten — der ct
            // soll aber bereits in CapturingStubHandler.SendAsync greifen.
            Responder = _ => Json(HttpStatusCode.OK,
                """{"is_relevant":false,"detections":[],"frame_class":"irrelevant","inference_time_ms":1.0}""")
        };
        var client = new VisionPipelineClient(BaseUri, new HttpClient(stub));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.DetectYoloAsync(new YoloRequest("base64data==", 0.3), cts.Token));

        // Stub darf nicht ausgeloest worden sein (Cancellation vor Senden).
        Assert.Empty(stub.Requests);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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
            // Cancellation respektieren — sonst koennen Cancellation-Tests
            // den Stub nicht zuverlaessig vor dem Senden abbrechen.
            cancellationToken.ThrowIfCancellationRequested();

            Requests.Add(request);
            var resp = Responder is null
                ? new HttpResponseMessage(HttpStatusCode.OK)
                : Responder(request);
            return Task.FromResult(resp);
        }
    }
}
