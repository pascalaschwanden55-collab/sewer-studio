namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerLastOpenedWindowOwner<TWindow>
    where TWindow : class
{
    private TWindow? _current;

    public TWindow? Current => _current;

    public bool HasCurrent => _current is not null;

    public void Set(TWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _current = window;
    }

    public bool IsCurrent(TWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return ReferenceEquals(_current, window);
    }

    public void Clear()
    {
        _current = null;
    }

    public bool ClearIfCurrent(TWindow window)
    {
        if (!IsCurrent(window))
            return false;

        Clear();
        return true;
    }
}
