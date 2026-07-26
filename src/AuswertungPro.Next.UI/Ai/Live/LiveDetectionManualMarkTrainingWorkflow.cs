using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Ai.Live;

public sealed record LiveDetectionManualMarkTrainingResult(
    bool Saved,
    string? Code,
    bool SessionEventAdded,
    bool PhotoPathAdded);

public static class LiveDetectionManualMarkTrainingWorkflow
{
    public static async Task<LiveDetectionManualMarkTrainingResult> SaveAsync(
        ProtocolEntry? selectedEntry,
        OverlayGeometry overlay,
        double timestampSec,
        string? clockPosition,
        string? displayedMeterText,
        ICodingSessionService? codingSessionService,
        byte[]? preCapturedFrame,
        Func<Task<byte[]?>> captureCurrentFrameAsync,
        Func<byte[], ProtocolEntry, OverlayGeometry, string?, double, TimeSpan, Task<TeacherAnnotation?>> saveManualMarkAsync,
        Action refreshEvents)
    {
        if (selectedEntry is null)
            return NotSaved(sessionEventAdded: false);

        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(saveManualMarkAsync);
        ArgumentNullException.ThrowIfNull(refreshEvents);

        var videoTimestamp = TimeSpan.FromSeconds(timestampSec);
        var captureMeter = CodingCurrentMeterResolver.ParseDisplayedMeterOrZero(displayedMeterText);

        CodingEvent? manualEvent = null;
        var sessionEventAdded = false;
        if (codingSessionService != null)
        {
            manualEvent = LiveDetectionManualMarkEventAppender.Apply(
                selectedEntry,
                captureMeter,
                videoTimestamp,
                overlay,
                codingSessionService);
            sessionEventAdded = true;
            refreshEvents();
        }

        var frameBytes = preCapturedFrame;
        if (frameBytes == null)
        {
            ArgumentNullException.ThrowIfNull(captureCurrentFrameAsync);
            frameBytes = await captureCurrentFrameAsync();
        }

        if (frameBytes == null)
            return NotSaved(sessionEventAdded);

        var annotation = await saveManualMarkAsync(
            frameBytes,
            selectedEntry,
            overlay,
            clockPosition,
            captureMeter,
            videoTimestamp);
        if (annotation == null)
            return NotSaved(sessionEventAdded);

        var photoPathAdded = manualEvent != null
                             && CodingProtocolEntryPhotoPathAppender.AddIfPresent(
                                 manualEvent.Entry,
                                 annotation.FullFramePath);
        if (photoPathAdded)
            refreshEvents();

        return new LiveDetectionManualMarkTrainingResult(
            Saved: true,
            selectedEntry.Code,
            sessionEventAdded,
            photoPathAdded);
    }

    private static LiveDetectionManualMarkTrainingResult NotSaved(bool sessionEventAdded)
        => new(Saved: false, Code: null, sessionEventAdded, PhotoPathAdded: false);
}
