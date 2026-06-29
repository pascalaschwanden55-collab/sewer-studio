using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunPreparationControllerTests
{
    [Fact]
    public void PrepareCancellation_startet_neue_cts_und_bricht_vorherige_ab()
    {
        var previousCts = new CancellationTokenSource();
        var previousToken = previousCts.Token;

        var result = SelfTrainingRunPreparationController.PrepareCancellation(previousCts);

        Assert.True(previousToken.IsCancellationRequested);
        Assert.NotNull(result.CancellationTokenSource);
        Assert.NotSame(previousCts, result.CancellationTokenSource);
        Assert.Equal(result.CancellationTokenSource.Token, result.CancellationToken);

        result.CancellationTokenSource.Dispose();
    }

    [Fact]
    public void PrepareCancellation_ohne_vorherige_cts_liefert_startbereiten_token()
    {
        var result = SelfTrainingRunPreparationController.PrepareCancellation(null);

        Assert.False(result.CancellationToken.IsCancellationRequested);
        Assert.Equal(result.CancellationTokenSource.Token, result.CancellationToken);

        result.CancellationTokenSource.Dispose();
    }
}
