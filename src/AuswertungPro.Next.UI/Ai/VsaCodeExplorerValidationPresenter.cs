namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerValidationPresentation(
    bool CanApply,
    string ValidationText,
    bool ShowValidation);

public static class VsaCodeExplorerValidationPresenter
{
    public static VsaCodeExplorerValidationPresentation Build(bool canConfirm, string? validationMessage)
    {
        var text = validationMessage ?? string.Empty;
        return new VsaCodeExplorerValidationPresentation(
            CanApply: canConfirm,
            ValidationText: text,
            ShowValidation: !string.IsNullOrWhiteSpace(text));
    }
}
