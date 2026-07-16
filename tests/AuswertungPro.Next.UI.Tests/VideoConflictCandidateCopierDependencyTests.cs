using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoConflictCandidateCopierDependencyTests
{
    [Fact]
    public void Kandidatenkopie_verwendet_die_zentrale_Dateiuebertragung()
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
        Assert.Same(
            services.VideoConflictCandidates,
            services.GetService(typeof(IVideoConflictCandidateCopier)));
    }

    [Fact]
    public void Statische_VideokonfliktFassade_ist_unveraenderbar()
    {
        var before = VideoConflictArtifacts.Current;
        var use = typeof(VideoConflictArtifacts).GetMethod(nameof(VideoConflictArtifacts.Use));
        var replacement = new VideoConflictCandidateCopyService(
            new DistributionFileTransferService());

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [replacement]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, VideoConflictArtifacts.Current);
    }
}
