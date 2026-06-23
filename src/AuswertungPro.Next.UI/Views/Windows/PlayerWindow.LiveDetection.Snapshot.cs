using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        return await LiveDetectionFrameCaptureServiceFactory.Create((path, width) => TakeSnapshotSafe(path, width))
            .CaptureAsync(
                () => _closing || _playbackDisposed,
                _detectionCts?.Token ?? CancellationToken.None);
    }
}
