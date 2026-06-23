using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CancellationTokenSourceLifecycleTests
{
    [Fact]
    public void CancelPreviousAndCreate_cancels_existing_source_and_returns_fresh_source()
    {
        using var previous = new CancellationTokenSource();
        var previousToken = previous.Token;

        using var current = CancellationTokenSourceLifecycle.CancelPreviousAndCreate(previous);

        Assert.True(previousToken.IsCancellationRequested);
        Assert.False(current.IsCancellationRequested);
        Assert.NotSame(previous, current);
    }

    [Fact]
    public void CancelDisposeAndClear_cancels_source_and_returns_null()
    {
        var previous = new CancellationTokenSource();
        var previousToken = previous.Token;

        var current = CancellationTokenSourceLifecycle.CancelDisposeAndClear(previous);

        Assert.Null(current);
        Assert.True(previousToken.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => previous.Token);
    }

    [Fact]
    public void CancelDisposeAndClear_handles_missing_source()
    {
        var current = CancellationTokenSourceLifecycle.CancelDisposeAndClear(null);

        Assert.Null(current);
    }
}
