using System.Reflection;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProcessOutputReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_Prozessausgaben_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<ProcessOutputReaderService>(services.ProcessOutputs);
        Assert.Same(
            services.ProcessOutputs,
            services.GetService(typeof(IProcessOutputReader)));

        var frameReaderField = typeof(VideoFrameExtractionService).GetField(
            "_processOutputs",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var pipelineReaderField = typeof(VideoAnalysisPipelineFactory).GetField(
            "_processOutputs",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(frameReaderField);
        Assert.NotNull(pipelineReaderField);
        Assert.Same(
            services.ProcessOutputs,
            frameReaderField!.GetValue(services.VideoFrameExtraction));
        Assert.Same(
            services.ProcessOutputs,
            pipelineReaderField!.GetValue(services.VideoAnalysisPipelines));

        var before = ProcessOutputReader.Current;
        var use = typeof(ProcessOutputReader).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.ProcessOutputs]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, ProcessOutputReader.Current);
    }
}
