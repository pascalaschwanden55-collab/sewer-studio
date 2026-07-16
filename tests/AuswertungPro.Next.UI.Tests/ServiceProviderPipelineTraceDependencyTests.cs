using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderPipelineTraceDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_Pipeline_Trace_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var field = typeof(VideoAnalysisPipelineFactory).GetField(
            "_pipelineTraceWriter",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(services.PipelineTrace, field!.GetValue(services.VideoAnalysisPipelines));
        Assert.Same(
            services.PipelineTrace,
            services.GetService(typeof(IPipelineTraceWriter)));

        var before = PipelineTraceWriter.Current;
        var use = typeof(PipelineTraceWriter).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.PipelineTrace]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, PipelineTraceWriter.Current);
    }
}
