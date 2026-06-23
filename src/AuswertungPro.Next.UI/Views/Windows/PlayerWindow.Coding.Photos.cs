using System;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
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

    private string? AttachBoundaryAnalyzedFramePhoto(ProtocolEntry entry, byte[]? analyzedFrameBytes)
    {
        return CodingAiFramePhotoService.AttachAnalyzedFramePhoto(
            entry,
            analyzedFrameBytes,
            _videoPath);
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

        var slotUpdate = CodingEventPhotoApplier.Apply(codingEvent, fotoPath, _codingSessionService);
        ShowOverlay(slotUpdate.OverlayText, TimeSpan.FromSeconds(3));

        RefreshCodingEventsList();
    }

    private void CodingTakePhoto_Click(object sender, RoutedEventArgs e) => CodingTakePhotoForSelectedEvent();

}
