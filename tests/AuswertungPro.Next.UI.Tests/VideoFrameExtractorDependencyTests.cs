using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoFrameExtractorDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_FrameExtraktor_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<VideoFrameExtractionService>(services.VideoFrameExtraction);
        Assert.Same(
            services.VideoFrameExtraction,
            services.GetService(typeof(IVideoFrameExtractor)));

        var before = VideoFrameExtractor.Current;
        var use = typeof(VideoFrameExtractor).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.VideoFrameExtraction]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, VideoFrameExtractor.Current);
    }
}
