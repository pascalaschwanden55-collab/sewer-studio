namespace AuswertungPro.Next.UI.Player;

public sealed class CodingProtocolStateControllerSet
{
    public CodingImportReferenceEventsOwner ImportReferenceEvents { get; } = new();

    public CodingNavigationPendingState NavigationPendingState { get; } = new();

    public CodingProtocolMatchStateController ProtocolMatchState { get; } = new();

    public CodingPendingConfirmationStateController PendingConfirmationState { get; } = new();

    public CodingBaselineSignatureStateController BaselineSignatureState { get; } = new();
}
