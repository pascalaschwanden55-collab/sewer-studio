using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DisposableReferenceLifecycleTests
{
    [Fact]
    public void DisposeAndClear_disposes_reference_and_returns_null()
    {
        var method = FindDisposeAndClearMethod();
        Assert.NotNull(method);
        var disposable = new TrackingDisposable();

        var result = method.MakeGenericMethod(typeof(TrackingDisposable)).Invoke(null, [disposable]);

        Assert.Null(result);
        Assert.True(disposable.Disposed);
    }

    [Fact]
    public void DisposeAndClear_handles_missing_reference()
    {
        var method = FindDisposeAndClearMethod();
        Assert.NotNull(method);

        var result = method.MakeGenericMethod(typeof(TrackingDisposable)).Invoke(null, [null]);

        Assert.Null(result);
    }

    private static MethodInfo? FindDisposeAndClearMethod()
        => typeof(PlayerWindowTimerFactory).Assembly
            .GetType("AuswertungPro.Next.UI.Player.DisposableReferenceLifecycle")
            ?.GetMethod(
                "DisposeAndClear",
                BindingFlags.Public | BindingFlags.Static);

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
            => Disposed = true;
    }
}
