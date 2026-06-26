using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        return await LiveDetectionFrameCaptureWorkflow.CaptureAsync(
            (path, width) => TakeSnapshotSafe(path, width),
            () => _shutdownState.IsUnavailable,
            _liveDetectionController.DetectionCancellation?.Token ?? CancellationToken.None);
    }
}
