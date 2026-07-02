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
                Content = new StringContent("""{"status":"ok","version":"1.0.0","gpu":null}""")
            });
        var service = CreateService(PipelineMode.Auto, multiModelEnabled: true, handler);

        var decision = await service.ShouldUseMultiModelAsync(CancellationToken.None);

        Assert.True(decision.UseMultiModel);
        Assert.Null(decision.FallbackReason);
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
