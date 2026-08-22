namespace AuswertungPro.Next.UI.ViewModels.Windows;

public partial class TrainingCenterViewModel : IDisposable
{
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        CancelOutstandingOperations();
        _kbHttpClient?.Dispose();
        _kbHttpClient = null;
    }
}
