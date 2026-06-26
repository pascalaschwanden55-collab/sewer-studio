using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionCorrectionCodeSelectionRequest(
    double? Meter,
    double TimestampSec,
    string? VideoPath,
    Window Owner);

public sealed record LiveDetectionCorrectionCodeSelectionActions(
    Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> CreateViewModel);

public static class LiveDetectionCorrectionCodeSelectionWorkflow
{
    public static ProtocolEntry? Select(
        LiveDetectionCorrectionCodeSelectionRequest request,
        LiveDetectionCorrectionCodeSelectionActions actions)
        => Select(
            request,
            actions,
            LiveDetectionCorrectionCodeSelectionServiceFactory.Create);

    public static ProtocolEntry? Select(
        LiveDetectionCorrectionCodeSelectionRequest request,
        LiveDetectionCorrectionCodeSelectionActions actions,
        Func<
            Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel>,
            LiveDetectionCorrectionCodeSelectionService> createService)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateViewModel);
        ArgumentNullException.ThrowIfNull(createService);

        var service = createService(actions.CreateViewModel);
        ArgumentNullException.ThrowIfNull(service);

        return service.Select(
            request.Meter,
            request.TimestampSec,
            request.VideoPath,
            request.Owner);
    }
}
