using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer das 503-Fehlerbody-Parsing des VisionPipelineClient (Paket 2/A4):
/// "insufficient_vram" ist ein Kapazitaetsfehler (eigener Typ, kein Retry),
/// "model_unloaded" bleibt gezielt transient (1 Retry), unbekannte/beschaedigte
/// Bodys laufen wie bisher ueber den allgemeinen 503-Weg.
/// </summary>
public class VisionPipelineClientVramErrorTests
{
    private const string DinoOkJson = """
        {
            "detections": [],
            "inference_time_ms": 1
        }
        """;

    private static VisionPipelineClient CreateClient(SequenceHandler handler)
        => new(new Uri("http://127.0.0.1:8100"), new HttpClient(handler))
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

    [Fact]
    public async Task Insufficient_vram_wirft_eigenen_fehlertyp_ohne_retry()
    {
        // EXAKT das Format des Sidecars (main.py @app.exception_handler(InsufficientVramError)):
        // code + Zahlen auf Top-Ebene, "detail" ist ein Klartext-String.
        var handler = new SequenceHandler((HttpStatusCode.ServiceUnavailable, """
            {"detail": "insufficient VRAM", "code": "insufficient_vram", "slot": "dino", "free_gb": 1.5, "required_gb": 4.25, "reserved_gb": 6.0}
            """));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SidecarInsufficientVramException>(
            () => client.DetectDinoAsync(new DinoRequest("abc", null, 0.25, 0.2)));

        Assert.Equal("/detect/dino", ex.Endpoint);
        Assert.Equal(1.5, ex.FreeGb);
        Assert.Equal(4.25, ex.RequiredGb);
        Assert.Equal(6.0, ex.ReservedGb);
        Assert.Contains("1.5", ex.Message);
        Assert.Contains("4.25", ex.Message);
        Assert.Contains("6", ex.Message);
        Assert.Equal(1, handler.Calls);   // KEIN HTTP-Retry bei einem Kapazitaetsfehler
    }

    [Fact]
    public async Task Vertrag_echtes_python_503_json_wird_erkannt()
    {
        // Vertragstest gegen das WOERTLICHE JSON, das der Python-Exception-Handler in
        // sidecar/sidecar/main.py (handle_insufficient_vram) ausliefert — inklusive dem
        // Klartext-"detail", das den eigentlichen Code NICHT enthaelt. Vor diesem Test
        // wurde fälschlich nur das verschachtelte Format {"detail": {"code": ...}} geprueft.
        var handler = new SequenceHandler((HttpStatusCode.ServiceUnavailable, """
            {"detail": "insufficient VRAM", "code": "insufficient_vram", "slot": "sam", "free_gb": 5.2, "required_gb": 16.0, "reserved_gb": 12.0}
            """));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SidecarInsufficientVramException>(
            () => client.SegmentSamAsync(new SamRequest("abc", Array.Empty<SamBoundingBox>(), 300)));

        Assert.Equal("/segment/sam", ex.Endpoint);
        Assert.Equal(5.2, ex.FreeGb);
        Assert.Equal(16.0, ex.RequiredGb);
        Assert.Equal(12.0, ex.ReservedGb);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Verschachteltes_detail_format_wird_toleriert()
    {
        // Toleranz (abwaerts): das alte verschachtelte Testformat bleibt lesbar.
        var handler = new SequenceHandler((HttpStatusCode.ServiceUnavailable, """
            {"detail": {"code": "insufficient_vram", "slot": "dino", "free_gb": 1.5, "required_gb": 4.25, "reserved_gb": 6.0}}
            """));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SidecarInsufficientVramException>(
            () => client.DetectDinoAsync(new DinoRequest("abc", null, 0.25, 0.2)));

        Assert.Equal("/detect/dino", ex.Endpoint);
        Assert.Equal(1.5, ex.FreeGb);
        Assert.Equal(4.25, ex.RequiredGb);
        Assert.Equal(6.0, ex.ReservedGb);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Insufficient_vram_als_string_detail_wird_erkannt()
    {
        var handler = new SequenceHandler((HttpStatusCode.ServiceUnavailable, """
            {"detail": "insufficient_vram"}
            """));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SidecarInsufficientVramException>(
            () => client.DetectYoloAsync(new YoloRequest("abc", 0.25)));

        Assert.Equal("/detect/yolo", ex.Endpoint);
        Assert.Null(ex.FreeGb);
        Assert.Null(ex.RequiredGb);
        Assert.Null(ex.ReservedGb);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Fehlende_vram_felder_werden_toleriert()
    {
        var handler = new SequenceHandler((HttpStatusCode.ServiceUnavailable, """
            {"detail": {"code": "insufficient_vram"}}
            """));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SidecarInsufficientVramException>(
            () => client.SegmentSamAsync(new SamRequest("abc", Array.Empty<SamBoundingBox>(), 300)));

        Assert.Equal("/segment/sam", ex.Endpoint);
        Assert.Null(ex.FreeGb);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Model_unloaded_bleibt_transient_mit_genau_einem_retry()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.ServiceUnavailable, """{"detail": {"code": "model_unloaded", "slot": "dino"}}"""),
            (HttpStatusCode.OK, DinoOkJson));
        var client = CreateClient(handler);

        var response = await client.DetectDinoAsync(new DinoRequest("abc", null, 0.25, 0.2));

        Assert.NotNull(response);
        Assert.Equal(2, handler.Calls);   // genau 1 Retry wie bisher
    }

    [Fact]
    public async Task Unbekannter_503_code_laeuft_ueber_den_alten_weg()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.ServiceUnavailable, """{"detail": {"code": "something_else"}}"""));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<SidecarUnavailableException>(
            () => client.DetectDinoAsync(new DinoRequest("abc", null, 0.25, 0.2)));

        Assert.Equal(2, handler.Calls);   // 1 Retry, danach ehrlich scheitern
    }

    [Fact]
    public async Task Beschaedigter_503_body_laeuft_ueber_den_alten_weg()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.ServiceUnavailable, "<html>kaputt</html>"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<SidecarUnavailableException>(
            () => client.DetectDinoAsync(new DinoRequest("abc", null, 0.25, 0.2)));

        Assert.Equal(2, handler.Calls);
    }

    /// <summary>Handler mit Antwortsequenz; das letzte Element wird wiederholt.</summary>
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Code, string Body)> _responses;

        public SequenceHandler(params (HttpStatusCode Code, string Body)[] responses)
            => _responses = new Queue<(HttpStatusCode, string)>(responses);

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var (code, body) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
