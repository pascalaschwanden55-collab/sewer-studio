namespace AuswertungPro.Next.UI.Player;

public sealed class CodingModeStateController
{
    public bool IsCodingMode { get; private set; }

    public void Set(bool enabled)
        => IsCodingMode = enabled;
}
