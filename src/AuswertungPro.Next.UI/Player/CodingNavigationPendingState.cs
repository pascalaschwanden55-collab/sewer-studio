namespace AuswertungPro.Next.UI.Player;

public sealed class CodingNavigationPendingState
{
    private bool _isPending;

    public bool IsPending => _isPending;

    public void MarkPending()
        => _isPending = true;

    public void Set(bool isPending)
        => _isPending = isPending;
}
