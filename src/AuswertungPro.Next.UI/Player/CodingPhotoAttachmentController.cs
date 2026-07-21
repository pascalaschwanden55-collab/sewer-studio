using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingPhotoAttachmentController
{
    void AttachAnalyzedFramePhoto(ProtocolEntry entry);

    Task<string?> AttachAnalyzedFramePhotoAsync(ProtocolEntry entry);

    string? AttachBoundaryAnalyzedFramePhoto(ProtocolEntry entry, byte[]? analyzedFrameBytes);

    CodingTakePhotoCommandWorkflowResult TakePhotoForSelectedEvent(object? selectedItem);
}

public sealed record CodingPhotoAttachmentControllerBindings(
    Func<Task<byte[]?>> GetPreferredFrameBytesAsync,
    Func<byte[]?> GetBufferedFrameBytes,
    Func<ProtocolEntry, byte[]?, string?> AttachAnalyzedFramePhoto,
    Func<ProtocolEntry, string?> CaptureSnapshot,
    Func<TimeSpan?> GetCurrentPlayerTimestamp,
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Action<string, TimeSpan> ShowOverlay,
    Action RefreshEvents);

/// <summary>
/// Verbindet analysierte Frames und manuell aufgenommene Fotos mit Coding-Ereignissen.
/// Das PlayerWindow liefert nur die aktuellen Laufzeit-Abhaengigkeiten.
/// </summary>
public sealed class CodingPhotoAttachmentController : ICodingPhotoAttachmentController
{
    private readonly CodingPhotoAttachmentControllerBindings _bindings;

    public CodingPhotoAttachmentController(CodingPhotoAttachmentControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.GetPreferredFrameBytesAsync);
        ArgumentNullException.ThrowIfNull(bindings.GetBufferedFrameBytes);
        ArgumentNullException.ThrowIfNull(bindings.AttachAnalyzedFramePhoto);
        ArgumentNullException.ThrowIfNull(bindings.CaptureSnapshot);
        ArgumentNullException.ThrowIfNull(bindings.GetCurrentPlayerTimestamp);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCodingSessionService);
        ArgumentNullException.ThrowIfNull(bindings.ShowOverlay);
        ArgumentNullException.ThrowIfNull(bindings.RefreshEvents);

        _bindings = bindings;
    }

    public void AttachAnalyzedFramePhoto(ProtocolEntry entry)
        => AttachAnalyzedFramePhotoAsync(entry).SafeFireAndForget("AttachAnalyzedFramePhoto");

    public async Task<string?> AttachAnalyzedFramePhotoAsync(ProtocolEntry entry)
    {
        var result = await CodingAnalyzedFramePhotoAttachmentWorkflow.ExecuteAsync(
            entry,
            new CodingAnalyzedFramePhotoAttachmentAsyncActions(
                _bindings.GetPreferredFrameBytesAsync,
                _bindings.GetBufferedFrameBytes,
                frameBytes => _bindings.AttachAnalyzedFramePhoto(entry, frameBytes),
                () => _bindings.CaptureSnapshot(entry)));

        if (!string.IsNullOrWhiteSpace(result.PhotoPath))
            _bindings.RefreshEvents();

        return result.PhotoPath;
    }

    public string? AttachBoundaryAnalyzedFramePhoto(
        ProtocolEntry entry,
        byte[]? analyzedFrameBytes)
        => _bindings.AttachAnalyzedFramePhoto(entry, analyzedFrameBytes);

    public CodingTakePhotoCommandWorkflowResult TakePhotoForSelectedEvent(object? selectedItem)
    {
        return CodingTakePhotoCommandWorkflow.Execute(
            selectedItem,
            new CodingTakePhotoCommandActions(
                GetCurrentPlayerTimestamp: _bindings.GetCurrentPlayerTimestamp,
                ApplyPhotoTimestamp: (codingEvent, timestamp) =>
                {
                    var photoTimestamp = CodingEventPhotoTimestampScope.Apply(codingEvent, timestamp);
                    return photoTimestamp.RestoreOriginalTime;
                },
                CaptureSnapshot: _bindings.CaptureSnapshot,
                ApplyPhoto: (codingEvent, photoPath) => CodingEventPhotoApplier.Apply(
                    codingEvent,
                    photoPath,
                    _bindings.ResolveCodingSessionService()),
                ShowOverlay: _bindings.ShowOverlay,
                RefreshCodingEventsList: _bindings.RefreshEvents));
    }
}
