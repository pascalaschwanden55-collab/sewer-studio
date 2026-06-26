namespace AuswertungPro.Next.UI.Player;

public sealed class CodingActiveToolNameStateController
{
    public string? ActiveToolName { get; private set; }

    public void Set(string? activeToolName)
        => ActiveToolName = activeToolName;

    public void Clear()
        => ActiveToolName = null;
}
