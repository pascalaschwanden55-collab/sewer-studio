using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingProtocolImportTrainingResult(
    bool Accepted,
    CodingImportConfirmationBadgeState Badge);

public sealed class CodingProtocolImportTrainingWorkflowService
{
    private readonly Func<CodingEvent, Task> _seekAndWait;
    private readonly Func<string?> _captureSnapshot;
    private readonly Action _showFrameCaptureFailed;
    private readonly Func<string> _createAnnotationId;
    private readonly CodingProtocolTrainingSnapshotStore _snapshotStore;
    private readonly Func<string, CodingEvent, string, TeacherAnnotation> _createAnnotation;
    private readonly Func<TeacherAnnotation, Task> _appendAnnotation;

    public CodingProtocolImportTrainingWorkflowService(
        Func<CodingEvent, Task> seekAndWait,
        Func<string?> captureSnapshot,
        Action showFrameCaptureFailed,
        Func<string> createAnnotationId,
        CodingProtocolTrainingSnapshotStore snapshotStore,
        Func<string, CodingEvent, string, TeacherAnnotation> createAnnotation,
        Func<TeacherAnnotation, Task> appendAnnotation)
    {
        _seekAndWait = seekAndWait ?? throw new ArgumentNullException(nameof(seekAndWait));
        _captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
        _showFrameCaptureFailed = showFrameCaptureFailed ?? throw new ArgumentNullException(nameof(showFrameCaptureFailed));
        _createAnnotationId = createAnnotationId ?? throw new ArgumentNullException(nameof(createAnnotationId));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _createAnnotation = createAnnotation ?? throw new ArgumentNullException(nameof(createAnnotation));
        _appendAnnotation = appendAnnotation ?? throw new ArgumentNullException(nameof(appendAnnotation));
    }

    public async Task<CodingProtocolImportTrainingResult> ConfirmAsync(CodingEvent importEvent)
    {
        await _seekAndWait(importEvent);

        var snapshotPath = _captureSnapshot();
        if (string.IsNullOrWhiteSpace(snapshotPath))
            return FailFrameCapture();

        var annotationId = _createAnnotationId();
        var destFrame = _snapshotStore.CopySnapshotToTrainingImages(snapshotPath, annotationId);
        if (destFrame == null)
            return FailFrameCapture();

        var annotation = _createAnnotation(annotationId, importEvent, destFrame);
        await _appendAnnotation(annotation);

        _snapshotStore.DeleteSnapshot(snapshotPath);
        var badge = CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge(
            importEvent.Entry.Code,
            importEvent.MeterAtCapture);
        return new CodingProtocolImportTrainingResult(true, badge);
    }

    private CodingProtocolImportTrainingResult FailFrameCapture()
    {
        _showFrameCaptureFailed();
        return new CodingProtocolImportTrainingResult(false, default);
    }
}
