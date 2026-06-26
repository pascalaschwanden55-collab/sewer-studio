namespace AuswertungPro.Next.UI.Player;

public sealed class CodingOverlayInputVisibilityStateController
{
    public int SuspendDepth { get; private set; }

    public bool WasOpenBeforeSuspend { get; private set; }

    public bool WasOpenBeforeExternalHide { get; private set; }

    public bool DeactivatedByExternalWindow { get; private set; }

    public void SetSuspendDepth(int depth)
        => SuspendDepth = depth;

    public void RememberOpenBeforeSuspend(bool isOpen)
        => WasOpenBeforeSuspend = isOpen;

    public void RememberOpenBeforeExternalHide(bool isOpen)
        => WasOpenBeforeExternalHide = isOpen;

    public void SetDeactivatedByExternalWindow(bool value)
        => DeactivatedByExternalWindow = value;

    public void ResetSuspendState()
    {
        SuspendDepth = 0;
        WasOpenBeforeSuspend = false;
    }
}
