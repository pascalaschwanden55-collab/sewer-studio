using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingPreviewFrameExtractorDependencyTests
{
    [Fact]
    public void ServiceProvider_und_VorschauFassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<TrainingPreviewFrameExtractionService>(services.TrainingPreviewFrames);
        Assert.Same(
            services.TrainingPreviewFrames,
            services.GetService(typeof(ITrainingPreviewFrameExtractor)));
        Assert.Same(services.TrainingPreviewFrames, TrainingPreviewFrameExtractor.Current);
    }
}
