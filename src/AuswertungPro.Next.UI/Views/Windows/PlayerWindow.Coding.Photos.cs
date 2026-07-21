using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void AttachAnalyzedFramePhoto(ProtocolEntry entry)
        => _codingPhotoAttachmentController.AttachAnalyzedFramePhoto(entry);

    private string? AttachBoundaryAnalyzedFramePhoto(ProtocolEntry entry, byte[]? analyzedFrameBytes)
        => _codingPhotoAttachmentController.AttachBoundaryAnalyzedFramePhoto(entry, analyzedFrameBytes);

    private void CodingTakePhotoForSelectedEvent()
        => _codingPhotoAttachmentController.TakePhotoForSelectedEvent(LstCodingEvents.SelectedItem);

    private void CodingTakePhoto_Click(object sender, RoutedEventArgs e) => CodingTakePhotoForSelectedEvent();
}
