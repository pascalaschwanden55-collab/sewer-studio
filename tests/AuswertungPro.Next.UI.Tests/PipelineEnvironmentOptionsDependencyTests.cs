using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipelineEnvironmentOptionsDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_Pipeline_Umgebung_direkt_und_Fassade_bleibt_unveraenderlich()
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
        var factory = Assert.IsType<VideoAnalysisPipelineFactory>(services.VideoAnalysisPipelines);
        var optionsField = typeof(VideoAnalysisPipelineFactory).GetField(
            "_pipelineEnvironmentOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(optionsField);
        Assert.Same(services.PipelineEnvironment, optionsField.GetValue(factory));

        var before = PipelineEnvironmentOptions.Current;
        var use = typeof(PipelineEnvironmentOptions).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.PipelineEnvironment]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, PipelineEnvironmentOptions.Current);
    }
}
