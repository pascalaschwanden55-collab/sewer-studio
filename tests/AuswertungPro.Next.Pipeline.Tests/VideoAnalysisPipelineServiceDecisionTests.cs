using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VideoAnalysisPipelineServiceDecisionTests
{
    [Fact]
    public async Task ShouldUseMultiModelAsync_OllamaOnly_SkipsSidecarHealthCheck()
    {
        var handler = new CountingHandler(_ => throw new InvalidOperationException("Health check should not run."));
        var service = CreateService(PipelineMode.OllamaOnly, multiModelEnabled: true, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.False(decision.UseMultiModel);
        Assert.Null(decision.FallbackReason);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_AutoWithKillSwitch_SkipsSidecarHealthCheck()
    {
        var handler = new CountingHandler(_ => throw new InvalidOperationException("Health check should not run."));
        var service = CreateService(PipelineMode.Auto, multiModelEnabled: false, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.False(decision.UseMultiModel);
        Assert.Null(decision.FallbackReason);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_Auto_WhenSidecarUnavailable_FallsBackWithWarning()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("offline")
            });
        var service = CreateService(PipelineMode.Auto, multiModelEnabled: true, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.False(decision.UseMultiModel);
        Assert.Contains("Ollama-Only", decision.FallbackReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_Auto_WhenSidecarHealthy_UsesMultiModel()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "status":"ok",
                  "version":"1.2.0",
                  "gpu":null,
                  "detector_qualification":{"qualified":true,"reason":null}
                }
                """)
            });
        var service = CreateService(PipelineMode.Auto, multiModelEnabled: true, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.True(decision.UseMultiModel);
        Assert.Null(decision.FallbackReason);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_MissingQualification_uses_DinoSam_with_review_warning()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "status":"ok",
                  "version":"1.2.0",
                  "gpu":null,
                  "models_present":{"dino":true,"sam":true}
                }
                """)
            });
        var service = CreateService(PipelineMode.Auto, multiModelEnabled: true, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.True(decision.UseMultiModel);
        Assert.Contains("Qualifikationsstatus", decision.FallbackReason);
        Assert.Contains("DINO/SAM", decision.FallbackReason);
        Assert.Contains("manuell", decision.FallbackReason);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_UnqualifiedDetector_keeps_DinoSam_with_warning()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "status":"degraded",
                  "version":"1.2.0",
                  "gpu":null,
                  "models_present":{"dino":true,"sam":true},
                  "detector_qualification":{"qualified":false,"reason":"BBox-Kollaps"}
                }
                """)
            });
        var service = CreateService(PipelineMode.Auto, multiModelEnabled: true, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.True(decision.UseMultiModel);
        Assert.Contains("DINO/SAM", decision.FallbackReason);
        Assert.Contains("manuell", decision.FallbackReason);
        Assert.DoesNotContain("Ollama-Only", decision.FallbackReason);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_Auto_WhenSidecarMissingDinoOrSam_FallsBackWithWarning()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {"status":"degraded","version":"1.2.0","gpu":null,"models_present":{"dino":false,"sam":true}}
                """)
            });
        var service = CreateService(PipelineMode.Auto, multiModelEnabled: true, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.False(decision.UseMultiModel);
        Assert.Contains("DINO", decision.FallbackReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ollama-Only", decision.FallbackReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_MultiModel_WhenSidecarUnavailable_Throws()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("offline")
            });
        var service = CreateService(PipelineMode.MultiModel, multiModelEnabled: false, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ShouldUseMultiModelAsync(CancellationToken.None));

        Assert.Contains("PipelineMode=MultiModel", ex.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_Auto_WhenSidecarVersionDiffers_FallsBackWithWarning()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"ok","version":"9.9.9","gpu":null}""")
            });
        var service = CreateService(PipelineMode.Auto, multiModelEnabled: true, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.False(decision.UseMultiModel);
        Assert.Contains("Version", decision.FallbackReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ollama-Only", decision.FallbackReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_MultiModel_WhenSidecarVersionDiffers_Throws()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"ok","version":"9.9.9","gpu":null}""")
            });
        var service = CreateService(PipelineMode.MultiModel, multiModelEnabled: true, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ShouldUseMultiModelAsync(CancellationToken.None));

        Assert.Contains("Version", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PipelineMode=MultiModel", ex.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ShouldUseMultiModelAsync_MultiModel_WhenSidecarMissingDinoOrSam_Throws()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {"status":"degraded","version":"1.2.0","gpu":null,"models_present":{"dino":true,"sam":false}}
                """)
            });
        var service = CreateService(PipelineMode.MultiModel, multiModelEnabled: false, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ShouldUseMultiModelAsync(CancellationToken.None));

        Assert.Contains("SAM", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PipelineMode=MultiModel", ex.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    private static VideoAnalysisPipelineService CreateService(
        PipelineMode mode,
        bool multiModelEnabled,
        HttpMessageHandler handler)
    {
        var runtime = new AiRuntimeSettings(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "qwen",
            TextModel: "qwen",
            EmbedModel: null,
            FfmpegPath: "ffmpeg",
            OllamaRequestTimeout: TimeSpan.FromSeconds(30),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);

        var pipeline = new PipelineConfig(
            MultiModelEnabled: multiModelEnabled,
            SidecarUrl: new Uri("http://localhost:8100"),
            SidecarToken: null,
            Mode: mode,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            DinoBoxThreshold: 0.3,
            DinoTextThreshold: 0.25,
            SidecarTimeoutSec: 30,
            PipeDiameterMmOverride: null);

        return new VideoAnalysisPipelineService(
            runtime,
            pipeline,
            new NoopAiSuggestionPlausibilityService(),
            new HttpClient(handler));
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public int RequestCount { get; private set; }

        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_respond(request));
        }
    }
}
