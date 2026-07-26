using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerValidationRenderTargets(
    Button ApplyButton,
    TextBlock ValidationText);

public static class VsaCodeExplorerValidationRenderer
{
    public static void Apply(
        VsaCodeExplorerValidationPresentation presentation,
        VsaCodeExplorerValidationRenderTargets targets)
    {
        targets.ApplyButton.IsEnabled = presentation.CanApply;
        targets.ValidationText.Text = presentation.ValidationText;
        targets.ValidationText.Visibility = presentation.ShowValidation ? Visibility.Visible : Visibility.Collapsed;
    }
}
