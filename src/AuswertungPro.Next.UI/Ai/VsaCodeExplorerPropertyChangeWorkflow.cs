using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerPropertyChangeActions(
    bool UpdateBreadcrumb = false,
    bool UpdateProgress = false,
    bool UpdateResultPanel = false,
    bool UpdateQuantPanel = false,
    bool UpdateClockPanel = false,
    bool SyncValidation = false,
    bool UpdateQ1Error = false,
    bool UpdateQ2Error = false);

public static class VsaCodeExplorerPropertyChangeWorkflow
{
    public static VsaCodeExplorerPropertyChangeActions Resolve(string? propertyName)
    {
        return propertyName switch
        {
            nameof(VsaCodeExplorerViewModel.CurrentLevel) => new(UpdateBreadcrumb: true, UpdateProgress: true),
            nameof(VsaCodeExplorerViewModel.CurrentGroupColor) => new(UpdateProgress: true),
            nameof(VsaCodeExplorerViewModel.ShowResultPanel) => new(UpdateProgress: true, UpdateResultPanel: true),
            nameof(VsaCodeExplorerViewModel.FinalCode) => new(UpdateProgress: true, UpdateResultPanel: true),
            nameof(VsaCodeExplorerViewModel.FinalLabel) => new(UpdateResultPanel: true),
            nameof(VsaCodeExplorerViewModel.FinalSublabel) => new(UpdateResultPanel: true),
            nameof(VsaCodeExplorerViewModel.WarnMessage) => new(UpdateResultPanel: true),
            nameof(VsaCodeExplorerViewModel.Q1Rule) => new(UpdateQuantPanel: true),
            nameof(VsaCodeExplorerViewModel.Q2Rule) => new(UpdateQuantPanel: true),
            nameof(VsaCodeExplorerViewModel.Q1Error) => new(UpdateQ1Error: true),
            nameof(VsaCodeExplorerViewModel.Q2Error) => new(UpdateQ2Error: true),
            nameof(VsaCodeExplorerViewModel.ClockMode) => new(UpdateClockPanel: true),
            nameof(VsaCodeExplorerViewModel.ClockHint) => new(UpdateClockPanel: true),
            nameof(VsaCodeExplorerViewModel.CanConfirm) => new(SyncValidation: true),
            nameof(VsaCodeExplorerViewModel.ValidationMessage) => new(SyncValidation: true),
            nameof(VsaCodeExplorerViewModel.BreadcrumbItems) => new(UpdateBreadcrumb: true),
            _ => new()
        };
    }
}
