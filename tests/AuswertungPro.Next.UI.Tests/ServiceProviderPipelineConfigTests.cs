using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderPipelineConfigTests
{
    [Fact]
    public void PipelineCfg_reflects_ai_start_defaults_applied_after_service_provider_construction()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings
        {
            AiEnabled = false,
            PipelineMultiModelEnabled = false,
            PipelineMode = "ollamaonly",
            AiOllamaUrl = "http://localhost:11434",
            PipelineSidecarUrl = "http://localhost:8100"
        };
        var provider = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Equal(PipelineMode.OllamaOnly, provider.PipelineCfg.Mode);

        AiStartupService.ApplyRuntimeDefaults(settings);

        Assert.Equal(PipelineMode.MultiModel, provider.PipelineCfg.Mode);
        Assert.True(provider.PipelineCfg.MultiModelEnabled);
    }
}
