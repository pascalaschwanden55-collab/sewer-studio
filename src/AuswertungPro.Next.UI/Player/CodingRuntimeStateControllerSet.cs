namespace AuswertungPro.Next.UI.Player;

public sealed class CodingRuntimeStateControllerSet
{
    public CodingModeStateController ModeState { get; } = new();

    public CodingSessionServiceOwner SessionRuntimeOwner { get; } = new();

    public CodingOverlayServiceOwner OverlayRuntimeOwner { get; } = new();
}
