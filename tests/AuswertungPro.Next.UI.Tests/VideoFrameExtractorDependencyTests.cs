using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoFrameExtractorDependencyTests
{
    [Fact]
    public void ServiceProvider_und_FrameFassade_verwenden_dieselbe_Instanz()
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
        Assert.Same(services.VideoFrameExtraction, VideoFrameExtractor.Current);
    }
}
