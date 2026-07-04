namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerFieldErrorPresentation(string Text, bool Show);

public static class VsaCodeExplorerFieldErrorPresenter
{
    public static VsaCodeExplorerFieldErrorPresentation Build(string? error)
        => new(error ?? "", error is not null);
}
