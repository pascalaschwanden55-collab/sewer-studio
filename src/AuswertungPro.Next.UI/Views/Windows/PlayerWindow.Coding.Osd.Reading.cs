using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;

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
        if (pngBytes.Length == 0)
            return null;

        try
        {
            var result = await GetCodingOsdMeterService().ReadMeterAsync(
                pngBytes,
                frameTimestampSec,
                _codingLastOsdMeter,
                _codingLastOsdTimestampSec,
                ct).ConfigureAwait(true);

            if (!result.Meter.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    System.Diagnostics.Debug.WriteLine($"[OSD] Frame-Meter nicht lesbar: {result.Error}");
                }
                else if (!string.IsNullOrWhiteSpace(result.RawReply) || result.Candidate.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[OSD] Meter verworfen. Raw='{result.RawReply}', Candidate={result.Candidate?.ToString("F2", CultureInfo.InvariantCulture) ?? "null"}, Last={result.RecentMeter?.ToString("F2", CultureInfo.InvariantCulture) ?? "null"}");
                }
                return null;
            }

            _codingLastOsdMeter = result.Meter.Value;
            _codingLastOsdTimestampSec = frameTimestampSec;
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = CodingOsdBadgeDisplayPolicy.BuildMeterText(result.Meter.Value);
            return result.Meter.Value;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OSD] Frame-Meter nicht lesbar: {ex.Message}");
            return null;
        }
    }

    private async Task<double?> CodingReadOsdMeterAsync()
    {
        if (_codingLiveDetection == null) return null;

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
