using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Player;

public sealed record LiveDetectionFrameCaptureWorkflowActions(
    Func<Func<string, uint, bool>, LiveDetectionFrameCaptureService> CreateService);

public static class LiveDetectionFrameCaptureWorkflow
{
    public static async Task<byte[]?> CaptureAsync(
        Func<string, uint, bool> takeSnapshot,
        Func<bool> isUnavailable,
        CancellationToken cancellationToken)
        => await CaptureAsync(
            takeSnapshot,
            isUnavailable,
            cancellationToken,
            new LiveDetectionFrameCaptureWorkflowActions(
                CreateService: LiveDetectionFrameCaptureServiceFactory.Create));

    public static async Task<byte[]?> CaptureAsync(
        Func<string, uint, bool> takeSnapshot,
        Func<bool> isUnavailable,
        CancellationToken cancellationToken,
        LiveDetectionFrameCaptureWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(takeSnapshot);
        ArgumentNullException.ThrowIfNull(isUnavailable);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService(takeSnapshot);
        ArgumentNullException.ThrowIfNull(service);

        return await service.CaptureAsync(isUnavailable, cancellationToken);
    }
}
