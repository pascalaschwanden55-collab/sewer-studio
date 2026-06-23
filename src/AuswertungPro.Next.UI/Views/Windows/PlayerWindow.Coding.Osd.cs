using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingOsdMeterService? _codingOsdMeterService;

    // True, wenn der zuletzt von ResolveCodingMeterForFrame gelieferte Meter aus dem OSD stammt
    // (Same-Frame oder frischer Cache), false bei linearer Schaetzung / CurrentMeter-Fallback.
    private bool _lastResolvedMeterIsOsd;

    private double? _codingLastOsdMeter;
    private double? _codingLastOsdTimestampSec;

    private CodingOsdMeterService GetCodingOsdMeterService()
        => _codingOsdMeterService ??= CodingOsdMeterService.CreateDefault();

    private void DisposeCodingOsdMeterService()
    {
        _codingOsdMeterService?.Dispose();
        _codingOsdMeterService = null;
    }

    private double ResolveCodingMeterForFrame(double? frameTimestampSeconds, double? sameFrameOsdMeter = null)
    {
        var durationSeconds = _player != null ? _player.Length / 1000.0 : (double?)null;
        var currentPlayerSeconds = _player != null ? _player.Time / 1000.0 : (double?)null;
        var result = CodingMeterResolver.Resolve(
            frameTimestampSeconds,
            sameFrameOsdMeter,
            _codingLastOsdMeter,
            _codingLastOsdTimestampSec,
            currentPlayerSeconds,
            durationSeconds,
            _codingVm?.EndMeter ?? 0,
            _codingVm?.CurrentMeter ?? 0);

        _lastResolvedMeterIsOsd = result.IsOsd;
        return result.Meter;
    }

    private double? GetMeterFromVideoPosition()
    {
        var currentPlayerSeconds = _player != null ? _player.Time / 1000.0 : (double?)null;
        var durationSeconds = _player != null ? _player.Length / 1000.0 : (double?)null;
        return CodingMeterResolver.EstimateFromVideo(
            currentPlayerSeconds,
            durationSeconds,
            _codingVm?.EndMeter ?? 0);
    }

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

            var pngBytes = await new CodingSnapshotCaptureService(path => TakeSnapshotSafe(path))
                .CapturePngAsync();
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

    private void StartCodingOsdTimer()
    {
        _codingOsdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _codingOsdTimer.Tick += async (_, _) =>
        {
            // Waehrend einer laufenden Live-Analyse liest diese bereits den OSD-Meter
            // -> separaten 3s-OSD-Timer aussetzen, um doppelte Qwen-Last zu vermeiden.
            if (!CodingOsdTimerPolicy.ShouldReadMeter(
                    _closing,
                    hasPlayer: _player is not null,
                    _isCodingMode,
                    _codingOsdReading,
                    _codingIsAnalyzing,
                    hasLiveDetection: _codingLiveDetection is not null))
                return;
            _codingOsdReading = true;
            try
            {
                await CodingReadOsdMeterAsync();
            }
            finally
            {
                _codingOsdReading = false;
            }
        };
        _codingOsdTimer.Start();
    }

    private void StopCodingOsdTimer()
    {
        _codingOsdTimer?.Stop();
        _codingOsdTimer = null;
        _codingOsdReading = false;
    }
}
