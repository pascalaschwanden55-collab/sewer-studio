using System.Threading;
using System.Threading.Tasks;
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
        var result = await CodingOsdMeterSnapshotWorkflow.ExecuteAsync(
            new CodingOsdMeterSnapshotWorkflowRequest(
                HasLiveDetection: _codingAiRuntimeOwner.Controller.LiveDetection != null,
                PlayerTimeMilliseconds: _player?.Time),
            new CodingOsdMeterSnapshotWorkflowActions(
                CaptureSnapshotAsync: () => CodingSnapshotCaptureFactory.CapturePngAsync(path => TakeSnapshotSafe(path)),
                ReadOsdMeterAsync: (pngBytes, timestampSeconds) => TryReadOsdMeterFromFrameBytesAsync(
                    pngBytes,
                    timestampSeconds,
                    CancellationToken.None)));

        return result.Meter;
    }
}
