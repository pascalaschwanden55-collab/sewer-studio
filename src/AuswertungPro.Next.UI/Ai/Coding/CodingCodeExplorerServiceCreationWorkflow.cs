using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingCodeExplorerServiceCreationWorkflowActions(
    Func<Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel>, CodingCodeExplorerWorkflowService> CreateService);

public static class CodingCodeExplorerServiceCreationWorkflow
{
    public static CodingCodeExplorerWorkflowService Create(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel)
        => Create(createViewModel, CodeUsageTrackers.Current);

    public static CodingCodeExplorerWorkflowService Create(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        ICodeUsageTracker codeUsage)
        => Create(
            createViewModel,
            new CodingCodeExplorerServiceCreationWorkflowActions(
                CreateService: factory => CodingCodeExplorerWorkflowServiceFactory.Create(factory, codeUsage)));

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
