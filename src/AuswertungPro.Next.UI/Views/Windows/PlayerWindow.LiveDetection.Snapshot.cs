using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        if (_closing || _playbackDisposed)
            return null;

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"sewer_live_{Guid.NewGuid():N}.png");
        try
        {
            var success = TakeSnapshotSafe(tempPath, 640);
            if (!success || _closing || _playbackDisposed)
                return null;

            // Wait briefly for file write
            await Task.Delay(80);

            if (!File.Exists(tempPath))
                return null;

            return await File.ReadAllBytesAsync(tempPath,
                _detectionCts?.Token ?? CancellationToken.None);
        }
        catch
        {
            return null;
        }
        finally
        {
            AuswertungPro.Next.Application.Common.BestEffort.Try(
                () => { if (File.Exists(tempPath)) File.Delete(tempPath); }, "Snapshot: Temp loeschen");
        }
    }
}
