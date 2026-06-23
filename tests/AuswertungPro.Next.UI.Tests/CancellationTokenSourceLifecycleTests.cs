using AuswertungPro.Next.UI.Player;
using System.Reflection;

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

    [Fact]
    public void CancelIfPresent_cancels_source_without_disposing_it()
    {
        var method = typeof(CancellationTokenSourceLifecycle).GetMethod(
            "CancelIfPresent",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CancellationTokenSource)],
            modifiers: null);
        Assert.NotNull(method);

        using var source = new CancellationTokenSource();
        var token = source.Token;

        method.Invoke(null, [source]);

        Assert.True(token.IsCancellationRequested);
        _ = source.Token;
    }

    [Fact]
    public void CancelIfPresent_handles_missing_source()
    {
        var method = typeof(CancellationTokenSourceLifecycle).GetMethod(
            "CancelIfPresent",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CancellationTokenSource)],
            modifiers: null);
        Assert.NotNull(method);

        method.Invoke(null, [null]);
    }
}
