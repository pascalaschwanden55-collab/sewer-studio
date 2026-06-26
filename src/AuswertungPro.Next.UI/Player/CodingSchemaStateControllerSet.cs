namespace AuswertungPro.Next.UI.Player;

public sealed class CodingSchemaStateControllerSet
{
    public CodingSchemaOverlayManagerOwner OverlayManagerOwner { get; } = new();

    public CodingSchemaTypeStateController TypeState { get; } = new();
}
