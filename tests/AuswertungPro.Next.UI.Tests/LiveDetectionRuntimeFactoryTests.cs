using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionRuntimeFactoryTests
{
    [Fact]
    public async Task CreateAsync_selects_available_vision_model()
    {
        var settings = CreateSettings("configured-model");

        var runtime = await LiveDetectionRuntimeFactory.CreateAsync(
            settings,
            createClient: cfg => new OllamaClient(cfg.OllamaBaseUri),
            listModelsAsync: (_, _) => Task.FromResult<IReadOnlyList<string>>(
                ["text-model:latest", "qwen3-vl:8b"]),
            createService: (client, model) => new LiveDetectionService(client, model),
            CancellationToken.None);

        try
        {
            Assert.Equal("qwen3-vl:8b", runtime.VisionModel);
            Assert.NotNull(runtime.Client);
            Assert.NotNull(runtime.Service);
        }
        finally
        {
            runtime.Client.Dispose();
        }
    }

    [Fact]
    public async Task CreateAsync_keeps_configured_model_when_model_listing_fails()
    {
        var settings = CreateSettings("configured-model");

        var runtime = await LiveDetectionRuntimeFactory.CreateAsync(
            settings,
            createClient: cfg => new OllamaClient(cfg.OllamaBaseUri),
            listModelsAsync: (_, _) => throw new InvalidOperationException("model list unavailable"),
            createService: (client, model) => new LiveDetectionService(client, model),
            CancellationToken.None);

        try
        {
            Assert.Equal("configured-model", runtime.VisionModel);
            Assert.NotNull(runtime.Client);
            Assert.NotNull(runtime.Service);
        }
        finally
        {
            runtime.Client.Dispose();
        }
    }

    private static AiRuntimeSettings CreateSettings(string visionModel)
        => new(
            Enabled: true,
            OllamaBaseUri: new Uri("http://127.0.0.1:11434"),
            VisionModel: visionModel,
            TextModel: "text-model",
            EmbedModel: null,
            FfmpegPath: null,
            OllamaRequestTimeout: TimeSpan.FromSeconds(30),
            OllamaKeepAlive: "24h",
            OllamaNumCtx: 4096);
}
