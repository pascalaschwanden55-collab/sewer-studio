using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionMarkCatalogDisplayRequest(
    string? ClockPosition,
    double TimestampSec,
    string? SuggestedCode,
    double? Meter,
    string? VideoPath,
    Window Owner,
    ICodeUsageTracker? CodeUsage = null);

public sealed record LiveDetectionMarkCatalogDisplayActions(
    Func<bool> HasCodeCatalog,
    Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> CreateViewModel,
    Action<ProtocolEntry> OnEntryCreated,
    Action<string> ShowOverlay);

public static class LiveDetectionMarkCatalogDisplayWorkflow
{
    public static bool TryOpen(
        LiveDetectionMarkCatalogDisplayRequest request,
        LiveDetectionMarkCatalogDisplayActions actions)
        => TryOpen(
            request,
            actions,
            (hasCodeCatalog, createViewModel, onEntryCreated, showOverlay) =>
                LiveDetectionMarkCatalogWorkflowServiceFactory.Create(
                    hasCodeCatalog,
                    createViewModel,
                    onEntryCreated,
                    showOverlay,
                    request.CodeUsage));

    public static bool TryOpen(
        LiveDetectionMarkCatalogDisplayRequest request,
        LiveDetectionMarkCatalogDisplayActions actions,
        Func<
            Func<bool>,
            Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel>,
            Action<ProtocolEntry>,
            Action<string>,
            LiveDetectionMarkCatalogWorkflowService> createService)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.HasCodeCatalog);
        ArgumentNullException.ThrowIfNull(actions.CreateViewModel);
        ArgumentNullException.ThrowIfNull(actions.OnEntryCreated);
        ArgumentNullException.ThrowIfNull(actions.ShowOverlay);
        ArgumentNullException.ThrowIfNull(createService);

        var service = createService(
            actions.HasCodeCatalog,
            actions.CreateViewModel,
            actions.OnEntryCreated,
            actions.ShowOverlay);
        ArgumentNullException.ThrowIfNull(service);

        return service.TryOpen(
            request.ClockPosition,
            request.TimestampSec,
            request.SuggestedCode,
            request.Meter,
            request.VideoPath,
            request.Owner);
    }
}
