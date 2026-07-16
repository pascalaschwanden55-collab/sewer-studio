using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingPreviewFrameExtractorDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_Trainingsvorschau_ohne_globalen_Umschalter()
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
        Assert.Null(typeof(TrainingPreviewFrameExtractor).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public));
    }
}
