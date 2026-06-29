using System.Threading;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunPreparationControllerTests
{
    [Fact]
    public void Prepare_wenn_busy_stoppt_ohne_status_oder_neue_cts()
    {
        var statusUpdates = new List<string>();

        var result = TrainingBatchImportRunPreparationController.Prepare(
            isBusy: true,
            rootFolderCount: 1,
            previousCancellationTokenSource: null,
            setStatus: statusUpdates.Add);

        Assert.True(result.ShouldStop);
        Assert.Equal(default, result.CancellationToken);
        Assert.Null(result.CancellationTokenSource);
        Assert.Empty(statusUpdates);
    }

    [Fact]
    public void Prepare_ohne_root_folder_stoppt_mit_status()
    {
        var statusUpdates = new List<string>();

        var result = TrainingBatchImportRunPreparationController.Prepare(
            isBusy: false,
            rootFolderCount: 0,
            previousCancellationTokenSource: null,
            setStatus: statusUpdates.Add);

        Assert.True(result.ShouldStop);
        Assert.Equal(default, result.CancellationToken);
        Assert.Null(result.CancellationTokenSource);
        Assert.Equal(new[] { TrainingFolderStatusBuilder.BuildMissingRootFolderStatus() }, statusUpdates);
    }

    [Fact]
    public void Prepare_startet_neue_cts_und_bricht_vorherige_ab()
    {
        var previousCts = new CancellationTokenSource();
        var previousToken = previousCts.Token;

        var result = TrainingBatchImportRunPreparationController.Prepare(
            isBusy: false,
            rootFolderCount: 2,
            previousCancellationTokenSource: previousCts,
            setStatus: _ => throw new InvalidOperationException("Status darf nicht gesetzt werden."));

        Assert.False(result.ShouldStop);
        Assert.True(previousToken.IsCancellationRequested);
        Assert.NotNull(result.CancellationTokenSource);
        Assert.NotSame(previousCts, result.CancellationTokenSource);
        Assert.Equal(result.CancellationTokenSource!.Token, result.CancellationToken);

        result.CancellationTokenSource.Dispose();
    }
}
