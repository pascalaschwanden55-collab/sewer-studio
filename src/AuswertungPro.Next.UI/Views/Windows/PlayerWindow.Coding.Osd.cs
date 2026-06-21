using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // True, wenn der zuletzt von ResolveCodingMeterForFrame gelieferte Meter aus dem OSD stammt
    // (Same-Frame oder frischer Cache), false bei linearer Schaetzung / CurrentMeter-Fallback.
    private bool _lastResolvedMeterIsOsd;

    private double? _codingLastOsdMeter;
    private double? _codingLastOsdTimestampSec;

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
            var croppedBytes = CodingOsdMeterReader.BuildOsdSearchImage(pngBytes);
            var config = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
            using var client = new OllamaClient(
                config.OllamaBaseUri,
                ownedTimeout: config.OllamaRequestTimeout,
                keepAlive: config.OllamaKeepAlive,
                numCtx: config.OllamaNumCtx);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var b64 = Convert.ToBase64String(croppedBytes);
            var messages = new[]
            {
                new OllamaClient.ChatMessage("user", CodingOsdMeterReader.Prompt, new[] { b64 })
            };
            var raw = await client.ChatAsync(config.VisionModel, messages, cts.Token);
            var candidate = CodingOsdMeterReader.ParseMeterReply(raw);

            var recentForJumpGuard = _codingLastOsdMeter;
            if (recentForJumpGuard.HasValue
                && CodingMeterResolver.ShouldResetRecentMeterForSeek(frameTimestampSec, _codingLastOsdTimestampSec))
            {
                recentForJumpGuard = null;
            }

            var meter = CodingOsdMeterReader.AcceptMeterCandidate(candidate, recentForJumpGuard);
            if (!meter.HasValue)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OSD] Meter verworfen. Raw='{raw}', Candidate={candidate?.ToString("F2", CultureInfo.InvariantCulture) ?? "null"}, Last={recentForJumpGuard?.ToString("F2", CultureInfo.InvariantCulture) ?? "null"}");
                return null;
            }

            _codingLastOsdMeter = meter.Value;
            _codingLastOsdTimestampSec = frameTimestampSec;
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"{meter.Value:F2}m (OSD)";
            return meter.Value;
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
}
