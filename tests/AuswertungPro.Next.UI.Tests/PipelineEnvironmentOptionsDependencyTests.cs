using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipelineEnvironmentOptionsDependencyTests
{
    [Fact]
    public void ServiceProvider_und_PipelineFabrik_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<PipelineEnvironmentOptionsService>(services.PipelineEnvironment);
        Assert.Same(
            services.PipelineEnvironment,
            services.GetService(typeof(IPipelineEnvironmentOptions)));
        Assert.Same(services.PipelineEnvironment, PipelineEnvironmentOptions.Current);

        var factory = Assert.IsType<VideoAnalysisPipelineFactory>(services.VideoAnalysisPipelines);
        var optionsField = typeof(VideoAnalysisPipelineFactory).GetField(
            "_pipelineEnvironmentOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(optionsField);
        Assert.Same(services.PipelineEnvironment, optionsField.GetValue(factory));
    }
}
