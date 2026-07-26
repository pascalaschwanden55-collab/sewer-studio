namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerStreckenschadenPresentation(
    bool ShowTypPanel,
    int? SelectedTypIndex);

public sealed record VsaCodeExplorerStreckenschadenChange(
    bool IsStreckenschaden,
    string StreckenschadenTyp,
    VsaCodeExplorerStreckenschadenPresentation Presentation);

public static class VsaCodeExplorerStreckenschadenWorkflow
{
    private const string DefaultTyp = "Anfang";
    private const string EndTyp = "Ende";

    public static VsaCodeExplorerStreckenschadenChange ApplyChecked(string? currentTyp)
        => BuildActive(currentTyp);

    public static VsaCodeExplorerStreckenschadenChange ApplyUnchecked()
        => new(
            IsStreckenschaden: false,
            StreckenschadenTyp: "",
            Presentation: new VsaCodeExplorerStreckenschadenPresentation(
                ShowTypPanel: false,
                SelectedTypIndex: null));

    public static VsaCodeExplorerStreckenschadenChange BuildInitial(
        bool isStreckenschaden,
        string? currentTyp)
        => isStreckenschaden
            ? BuildActive(currentTyp)
            : ApplyUnchecked();

    public static string ApplySelectionChanged(string? selectedText)
        => selectedText ?? "";

    private static VsaCodeExplorerStreckenschadenChange BuildActive(string? currentTyp)
    {
        var typ = string.IsNullOrWhiteSpace(currentTyp) ? DefaultTyp : currentTyp;
        return new VsaCodeExplorerStreckenschadenChange(
            IsStreckenschaden: true,
            StreckenschadenTyp: typ,
            Presentation: new VsaCodeExplorerStreckenschadenPresentation(
                ShowTypPanel: true,
                SelectedTypIndex: ResolveTypIndex(typ)));
    }

    private static int ResolveTypIndex(string typ)
        => string.Equals(typ, EndTyp, System.StringComparison.Ordinal) ? 1 : 0;
}
