using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync(
        byte[] pngBytes,
        double frameTimestampSec,
        CancellationToken ct)
    {
        return await TryReadOsdMeterFromFrameBytesAsync(
            pngBytes,
            frameTimestampSec,
            ct).ConfigureAwait(true);
    }

    private async Task<double?> TryReadOsdMeterFromFrameBytesAsync(
        byte[] pngBytes,
        double? frameTimestampSec,
        CancellationToken ct)
    {
        var result = await CodingOsdMeterReadWorkflow.ExecuteAsync(
            new CodingOsdMeterReadWorkflowRequest(
                pngBytes,
                frameTimestampSec,
                _codingOsdMeterController.LastMeter,
                _codingOsdMeterController.LastTimestampSeconds,
                ct),
            new CodingOsdMeterReadWorkflowActions(
                ReadMeterAsync: (bytes, timestamp, lastMeter, lastTimestamp, cancellationToken) =>
                    GetCodingOsdMeterService().ReadMeterAsync(
                        bytes,
                        timestamp,
                        lastMeter,
                        lastTimestamp,
                        cancellationToken),
                ApplyMeterState: ApplyCodingOsdMeterState,
                Trace: message => PlayerTrace.WriteLine(message)));

        return result.Meter;
    }

    private async Task<double?> CodingReadOsdMeterAsync()
    {
        if (_codingAiController.LiveDetection == null) return null;

        try
        {
            var snapshotTimestampSec = _player != null && _player.Time >= 0
                ? _player.Time / 1000.0
                : (double?)null;

            var pngBytes = await CodingSnapshotCaptureFactory.CapturePngAsync(path => TakeSnapshotSafe(path));
            if (pngBytes == null || pngBytes.Length == 0)
                return null;

            return await TryReadOsdMeterFromFrameBytesAsync(
                pngBytes,
                snapshotTimestampSec,
                CancellationToken.None);
        }
        catch
        {
            return null;
        }
    }
}
