namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class SelfTrainingCancellationController
{
    private CancellationTokenSource? _current;

    public CancellationToken Reset()
    {
        _current?.Cancel();
        _current?.Dispose();
        _current = new CancellationTokenSource();
        return _current.Token;
    }

    public void Cancel()
    {
        _current?.Cancel();
    }
}
