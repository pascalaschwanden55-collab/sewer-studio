using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoConflictCandidateCopierDependencyTests
{
    [Fact]
    public void Kandidatenkopie_und_Fassade_verwenden_die_zentrale_Dateiuebertragung()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var copier = Assert.IsType<VideoConflictCandidateCopyService>(services.VideoConflictCandidates);
        var transferField = typeof(VideoConflictCandidateCopyService).GetField(
            "_fileTransfer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(transferField);
        Assert.Same(services.DistributionFileTransfers, transferField!.GetValue(copier));
        Assert.Same(services.VideoConflictCandidates, VideoConflictArtifacts.Current);
        Assert.Same(
            services.VideoConflictCandidates,
            services.GetService(typeof(IVideoConflictCandidateCopier)));
    }
}
