using System;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private string? AttachAnalyzedFramePhoto(ProtocolEntry entry)
    {
        var result = CodingAnalyzedFramePhotoAttachmentWorkflow.Execute(
            entry,
            new CodingAnalyzedFramePhotoAttachmentActions(
                GetPreferredFrameBytes: TryExtractAnalyzedFrameBytes,
                GetBufferedFrameBytes: () => _liveDetectionController.PendingConfirmationFrameBytes,
                AttachAnalyzedFramePhoto: frameBytes => CodingAnalyzedFramePhotoAttacher.Attach(
                    entry,
                    frameBytes,
                    _playbackContext.VideoPath),
                CaptureSnapshot: () => CodingCaptureSnapshot(entry)));

        return result.PhotoPath;
    }

    private string? AttachBoundaryAnalyzedFramePhoto(ProtocolEntry entry, byte[]? analyzedFrameBytes)
    {
        return CodingAnalyzedFramePhotoAttacher.Attach(
            entry,
            analyzedFrameBytes,
            _playbackContext.VideoPath);
    }

    private void CodingTakePhotoForSelectedEvent()
        => CodingTakePhotoCommandWorkflow.Execute(
            LstCodingEvents.SelectedItem,
            new CodingTakePhotoCommandActions(
                GetCurrentPlayerTimestamp: GetCurrentPlayerTimestamp,
                ApplyPhotoTimestamp: (codingEvent, timestamp) =>
                {
                    var photoTimestamp = CodingEventPhotoTimestampScope.Apply(codingEvent, timestamp);
                    return photoTimestamp.RestoreOriginalTime;
                },
                CaptureSnapshot: CodingCaptureSnapshot,
                ApplyPhoto: (codingEvent, fotoPath) =>
                    CodingEventPhotoApplier.Apply(
                        codingEvent,
                        fotoPath,
                        _codingSessionRuntimeOwner.Service),
                ShowOverlay: ShowOverlay,
                RefreshCodingEventsList: RefreshCodingEventsList));

    private void CodingTakePhoto_Click(object sender, RoutedEventArgs e) => CodingTakePhotoForSelectedEvent();

}
