using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private string? AttachAnalyzedFramePhoto(ProtocolEntry entry)
    {
        var frameBytes = TryExtractAnalyzedFrameBytes() ?? _detectionPendingFrameBytes;

        var path = CodingAiFramePhotoService.AttachAnalyzedFramePhoto(
            entry,
            frameBytes,
            _videoPath);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        var fallback = CodingCaptureSnapshot(entry);
        if (!string.IsNullOrWhiteSpace(fallback)
            && !entry.FotoPaths.Contains(fallback, StringComparer.OrdinalIgnoreCase))
        {
            entry.FotoPaths.Add(fallback);
        }

        return fallback;
    }

    private byte[]? TryExtractAnalyzedFrameBytes()
    {
        var sec = CodingAnalyzedFrameTimestampPolicy.Resolve(
            _detectionPendingTimestampSec,
            _codingFrameReadiness.FirstCleanFrameSeconds);
        return TryExtractFrameAtSeconds(sec);
    }

    private byte[]? TryExtractFrameAtSeconds(double? sec)
    {
        if (sec is null || sec.Value < 0 || string.IsNullOrWhiteSpace(_videoPath))
            return null;

        try
        {
            var ffmpeg = FfmpegLocator.ResolveFfmpeg();
            if (string.IsNullOrWhiteSpace(ffmpeg))
                return null;

            return VideoFrameExtractor.TryExtractFramePngAsync(
                ffmpeg, _videoPath, TimeSpan.FromSeconds(sec.Value), CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Foto] ffmpeg-Frame-Extraktion fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    private string? AttachBoundaryAnalyzedFramePhoto(ProtocolEntry entry, byte[]? analyzedFrameBytes)
    {
        return CodingAiFramePhotoService.AttachAnalyzedFramePhoto(
            entry,
            analyzedFrameBytes,
            _videoPath);
    }

    private TimeSpan? GetCurrentPlayerTimestamp()
    {
        if (_player == null || _player.Time < 0)
            return null;

        return TimeSpan.FromMilliseconds(_player.Time);
    }

    private string? CodingCaptureSnapshot(ProtocolEntry entry)
    {
        try
        {
            var target = CodingSnapshotTargetPolicy.Build(entry, _videoPath, DateTimeOffset.Now);
            Directory.CreateDirectory(target.PhotoDirectory);

            TakeSnapshotSafe(target.FilePath);

            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(50);
                if (File.Exists(target.FilePath) && new FileInfo(target.FilePath).Length > 100)
                    return target.FilePath;
            }

            return File.Exists(target.FilePath) ? target.FilePath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snapshot-Fehler: {ex.Message}");
            return null;
        }
    }

    private void CodingTakePhotoForSelectedEvent()
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        var entry = codingEvent.Entry;
        var originalZeit = entry.Zeit;
        var originalVideoTimestamp = codingEvent.VideoTimestamp;
        var photoTime = GetCurrentPlayerTimestamp();
        if (photoTime.HasValue)
        {
            entry.Zeit = photoTime.Value;
            codingEvent.VideoTimestamp = photoTime.Value;
        }

        var fotoPath = CodingCaptureSnapshot(entry);
        if (fotoPath == null)
        {
            entry.Zeit = originalZeit;
            codingEvent.VideoTimestamp = originalVideoTimestamp;
            ShowOverlay("Foto konnte nicht aufgenommen werden", TimeSpan.FromSeconds(3));
            return;
        }

        var slotUpdate = CodingPhotoSlotPolicy.Apply(entry.FotoPaths, fotoPath);
        ShowOverlay(slotUpdate.OverlayText, TimeSpan.FromSeconds(3));

        _codingSessionService?.UpdateEvent(codingEvent.EventId, entry, codingEvent.Overlay);
        RefreshCodingEventsList();
    }

    private void CodingTakePhoto_Click(object sender, RoutedEventArgs e) => CodingTakePhotoForSelectedEvent();

}
