namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerWindowShutdownStateController
{
    private volatile bool _isClosing;
    private bool _isPlaybackDisposed;

    public bool IsClosing => _isClosing;

    public bool IsPlaybackDisposed => _isPlaybackDisposed;

    public bool IsUnavailable => IsClosing || IsPlaybackDisposed;

    public void MarkClosing()
    {
        _isClosing = true;
    }

    public void MarkPlaybackDisposed()
    {
        _isPlaybackDisposed = true;
    }
}
