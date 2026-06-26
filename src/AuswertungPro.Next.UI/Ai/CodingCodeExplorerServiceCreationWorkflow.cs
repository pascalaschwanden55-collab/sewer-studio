using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingCodeExplorerServiceCreationWorkflowActions(
    Func<Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel>, CodingCodeExplorerWorkflowService> CreateService);

public static class CodingCodeExplorerServiceCreationWorkflow
{
    public static CodingCodeExplorerWorkflowService Create(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel)
        => Create(
            createViewModel,
            new CodingCodeExplorerServiceCreationWorkflowActions(
                CreateService: CodingCodeExplorerWorkflowServiceFactory.Create));

    public static CodingCodeExplorerWorkflowService Create(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        CodingCodeExplorerServiceCreationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(createViewModel);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService(createViewModel);
        ArgumentNullException.ThrowIfNull(service);

        return service;
    }
}
