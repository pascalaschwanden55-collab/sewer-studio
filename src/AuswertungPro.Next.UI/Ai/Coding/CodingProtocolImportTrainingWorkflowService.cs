using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingProtocolImportTrainingResult(
    bool Accepted,
    CodingImportConfirmationBadgeState Badge,
    CodingProtocolVerificationResult? Verification = null);

public sealed record CodingProtocolVerificationResult(
    string ConfirmationLevel,
    bool DamageVisible,
    string? ActualCode,
    double? MeterReading,
    string Explanation);

public sealed class CodingProtocolImportTrainingWorkflowService
{
    private readonly Func<CodingEvent, Task> _seekAndWait;
    private readonly Func<string?> _captureSnapshot;
    private readonly Action _showFrameCaptureFailed;
    private readonly Func<string> _createAnnotationId;
    private readonly CodingProtocolTrainingSnapshotStore _snapshotStore;
    private readonly Func<string, CodingEvent, string, TeacherAnnotation> _createAnnotation;
    private readonly Func<TeacherAnnotation, Task> _appendAnnotation;
    private readonly Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>>? _verifyProtocolAsync;

    public CodingProtocolImportTrainingWorkflowService(
        Func<CodingEvent, Task> seekAndWait,
        Func<string?> captureSnapshot,
        Action showFrameCaptureFailed,
        Func<string> createAnnotationId,
        CodingProtocolTrainingSnapshotStore snapshotStore,
        Func<string, CodingEvent, string, TeacherAnnotation> createAnnotation,
        Func<TeacherAnnotation, Task> appendAnnotation,
        Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>>? verifyProtocolAsync = null)
    {
        _seekAndWait = seekAndWait ?? throw new ArgumentNullException(nameof(seekAndWait));
        _captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
        _showFrameCaptureFailed = showFrameCaptureFailed ?? throw new ArgumentNullException(nameof(showFrameCaptureFailed));
        _createAnnotationId = createAnnotationId ?? throw new ArgumentNullException(nameof(createAnnotationId));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _createAnnotation = createAnnotation ?? throw new ArgumentNullException(nameof(createAnnotation));
        _appendAnnotation = appendAnnotation ?? throw new ArgumentNullException(nameof(appendAnnotation));
        _verifyProtocolAsync = verifyProtocolAsync;
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

        var verification = await VerifyProtocolAsync(destFrame, importEvent);
        var annotation = _createAnnotation(annotationId, importEvent, destFrame);
        ApplyVerification(annotation, verification);
        await _appendAnnotation(annotation);

        _snapshotStore.DeleteSnapshot(snapshotPath);
        var badge = CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge(
            importEvent.Entry.Code,
            importEvent.MeterAtCapture,
            verification);
        return new CodingProtocolImportTrainingResult(true, badge, verification);
    }

    private async Task<CodingProtocolVerificationResult?> VerifyProtocolAsync(string framePath, CodingEvent importEvent)
    {
        if (_verifyProtocolAsync is null)
            return null;

        try
        {
            return await _verifyProtocolAsync(framePath, importEvent);
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyVerification(
        TeacherAnnotation annotation,
        CodingProtocolVerificationResult? verification)
    {
        if (verification is null)
            return;

        annotation.ProtocolVerificationLevel = verification.ConfirmationLevel;
        annotation.ProtocolDamageVisible = verification.DamageVisible;
        annotation.ProtocolVerificationCode = verification.ActualCode;
        annotation.ProtocolVerificationMeter = verification.MeterReading;
        annotation.ProtocolVerificationExplanation = verification.Explanation;
    }

    private CodingProtocolImportTrainingResult FailFrameCapture()
    {
        _showFrameCaptureFailed();
        return new CodingProtocolImportTrainingResult(false, default);
    }
}
