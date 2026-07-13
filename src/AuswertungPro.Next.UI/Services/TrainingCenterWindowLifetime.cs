namespace AuswertungPro.Next.UI.Services;

internal sealed class TrainingCenterWindowLifetime : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private int _disposed;

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _source.Cancel();
        _source.Dispose();
    }
}
