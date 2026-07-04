namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerColumnLayoutPresentation(bool ShowChar2Column);

public static class VsaCodeExplorerColumnLayoutPresenter
{
    public static VsaCodeExplorerColumnLayoutPresentation Build(int char2TileCount)
        => new(ShowChar2Column: char2TileCount > 0);
}
