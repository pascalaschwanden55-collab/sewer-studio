using System.Threading;

namespace AuswertungPro.Next.UI.Player;

public static class CancellationTokenSourceLifecycle
{
    public static void CancelIfPresent(CancellationTokenSource? current)
    {
        current?.Cancel();
    }

    public static CancellationTokenSource CancelPreviousAndCreate(CancellationTokenSource? current)
    {
        current?.Cancel();
        return new CancellationTokenSource();
    }

    public static CancellationTokenSource? CancelDisposeAndClear(CancellationTokenSource? current)
    {
        current?.Cancel();
        current?.Dispose();
        return null;
    }
}
