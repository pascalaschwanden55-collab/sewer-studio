using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunControlControllerTests
{
    [Fact]
    public void RequestCancel_bricht_cts_ab_und_liefert_status()
    {
        using var cts = new CancellationTokenSource();

        var status = TrainingBatchImportRunControlController.RequestCancel(cts);

        Assert.True(cts.IsCancellationRequested);
        Assert.Equal("Abbruch angefordert...", status);
    }

    [Fact]
    public void RequestCancel_ohne_cts_liefert_status()
    {
        var status = TrainingBatchImportRunControlController.RequestCancel(null);

        Assert.Equal("Abbruch angefordert...", status);
    }
}
