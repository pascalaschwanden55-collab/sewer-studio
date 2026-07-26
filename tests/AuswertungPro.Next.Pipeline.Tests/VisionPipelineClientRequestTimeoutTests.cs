using System;
using System.Diagnostics;
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
/// Tests fuer das Per-Request-Timeout der Sidecar-Inferenzaufrufe (Paket 3/C):
/// kurzer Cap entkoppelt vom geteilten 5-min-Client-Timeout; Timeout = Transportfehler.
/// </summary>
[Collection("EnvironmentVars")]
public class VisionPipelineClientRequestTimeoutTests
{
    [Fact]
    public async Task Inferenz_requesttimeout_wirft_SidecarRequestTimeoutException_ohne_retry()
    {
        var handler = new HangingHandler();
        var client = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), new HttpClient(handler))
        {
            RequestTimeout = TimeSpan.FromMilliseconds(100)
        };

        var sw = Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<SidecarRequestTimeoutException>(
            () => client.DetectYoloAsync(new YoloRequest("abc", 0.25)));
        sw.Stop();

        Assert.Equal("/detect/yolo", ex.Endpoint);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"Cap muss schnell ausloesen: {sw.Elapsed}");
        Assert.Equal(1, handler.Calls);   // kein Retry nach einem Timeout (Sidecar haengt vermutlich)
    }

    [Fact]
    public async Task Dino_und_sam_nutzen_dasselbe_request_cap()
    {
        var client = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), new HttpClient(new HangingHandler()))
        {
            RequestTimeout = TimeSpan.FromMilliseconds(100)
        };

        await Assert.ThrowsAsync<SidecarRequestTimeoutException>(
            () => client.DetectDinoAsync(new DinoRequest("abc", null, 0.25, 0.2)));
        await Assert.ThrowsAsync<SidecarRequestTimeoutException>(
            () => client.SegmentSamAsync(new SamRequest("abc", Array.Empty<SamBoundingBox>(), 300)));
        await Assert.ThrowsAsync<SidecarRequestTimeoutException>(
            () => client.ClassifyYoloAsync(new YoloClassifyRequest("abc", 1)));
    }

    [Fact]
    public async Task Abbruch_durch_aufrufer_bleibt_OperationCanceledException()
    {
        var client = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), new HttpClient(new HangingHandler()))
        {
            RequestTimeout = TimeSpan.FromHours(1)
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Nutzerabbruch darf NICHT als Timeout-Transportfehler umgelabelt werden.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.DetectYoloAsync(new YoloRequest("abc", 0.25), cts.Token));
    }

    [Fact]
    public async Task Gesunder_inferenz_request_laeuft_trotz_kurzem_caps_normal_durch()
    {
        var json = """
        {
            "is_relevant": true,
            "detections": [],
            "frame_class": "relevant",
            "inference_time_ms": 5,
            "model_name": "m.pt",
            "device": "cpu",
            "queue_wait_ms": 0,
            "vram_allocated_gb": 0,
            "vram_total_gb": 31.5
        }
        """;
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(new StaticResponseHandler(json)))
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

        var response = await client.DetectYoloAsync(new YoloRequest("abc", 0.25));

        Assert.True(response.IsRelevant);
    }

    [Fact]
    public void Env_variable_setzt_das_request_timeout()
    {
        var previous = Environment.GetEnvironmentVariable(VisionPipelineClient.RequestTimeoutEnvVar);
        var previousCompat = Environment.GetEnvironmentVariable("AUSWERTUNGPRO_SIDECAR_REQUEST_TIMEOUT_SEC");
        Environment.SetEnvironmentVariable(VisionPipelineClient.RequestTimeoutEnvVar, "42");
        Environment.SetEnvironmentVariable("AUSWERTUNGPRO_SIDECAR_REQUEST_TIMEOUT_SEC", null);
        try
        {
            var client = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), new HttpClient());
            Assert.Equal(TimeSpan.FromSeconds(42), client.RequestTimeout);
        }
        finally
        {
            Environment.SetEnvironmentVariable(VisionPipelineClient.RequestTimeoutEnvVar, previous);
            Environment.SetEnvironmentVariable("AUSWERTUNGPRO_SIDECAR_REQUEST_TIMEOUT_SEC", previousCompat);
        }
    }

    [Fact]
    public void Ungueltige_env_variable_faellt_auf_default_120s()
    {
        var previous = Environment.GetEnvironmentVariable(VisionPipelineClient.RequestTimeoutEnvVar);
        Environment.SetEnvironmentVariable(VisionPipelineClient.RequestTimeoutEnvVar, "kaputt");
        try
        {
            var client = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), new HttpClient());
            Assert.Equal(VisionPipelineClient.DefaultRequestTimeout, client.RequestTimeout);
            Assert.Equal(TimeSpan.FromSeconds(120), client.RequestTimeout);
        }
        finally
        {
            Environment.SetEnvironmentVariable(VisionPipelineClient.RequestTimeoutEnvVar, previous);
        }
    }

    [Fact]
    public async Task Timeout_meldung_nennt_modell_und_endpunkt_ohne_token()
    {
        var client = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), new HttpClient(new HangingHandler()))
        {
            RequestTimeout = TimeSpan.FromMilliseconds(100)
        };

        var ex = await Assert.ThrowsAsync<SidecarRequestTimeoutException>(
            () => client.DetectDinoAsync(new DinoRequest("abc", null, 0.25, 0.2)));

        Assert.Equal("DINO", ex.Model);
        Assert.Equal("/detect/dino", ex.Endpoint);
        Assert.Contains("DINO", ex.Message);
        Assert.Contains("/detect/dino", ex.Message);
        Assert.DoesNotContain("token", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Handler, der bis zum Abbruch haengt (simuliert den festen CUDA-Call).</summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        public int Calls;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException("Der Abbruch-Token muss das Delay beendet haben.");
        }
    }

    private sealed class StaticResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
