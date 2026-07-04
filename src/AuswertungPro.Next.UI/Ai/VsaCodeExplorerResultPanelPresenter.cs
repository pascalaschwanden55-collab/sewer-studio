namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerResultPanelPresentation(
    bool ShowResultPanel,
    bool ShowCodeHintPanel,
    bool ShouldUpdateDetailPanels,
    string FinalCodeText,
    string FinalLabelText,
    string WarnText,
    bool ShowWarn);

public static class VsaCodeExplorerResultPanelPresenter
{
    public static VsaCodeExplorerResultPanelPresentation Build(
        bool showResultPanel,
        string? finalCode,
        string? finalLabel,
        string? finalSublabel,
        string? warnMessage)
    {
        if (!showResultPanel)
            return new VsaCodeExplorerResultPanelPresentation(false, true, false, "", "", "", false);

        var labelText = finalLabel ?? "";
        if (finalSublabel is not null)
            labelText += $" \u2014 {finalSublabel}";

        return new VsaCodeExplorerResultPanelPresentation(
            ShowResultPanel: true,
            ShowCodeHintPanel: false,
            ShouldUpdateDetailPanels: true,
            FinalCodeText: finalCode ?? "",
            FinalLabelText: labelText,
            WarnText: warnMessage ?? "",
            ShowWarn: !string.IsNullOrEmpty(warnMessage));
    }
}
