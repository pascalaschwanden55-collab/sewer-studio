using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Ai.Teacher;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolImportTrainingWorkflowServiceFactory
{
    public static CodingProtocolImportTrainingWorkflowService Create(
        Action<CodingEvent> seekToImportEvent,
        Func<string?> captureSnapshot,
        Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>>? verifyProtocolAsync = null)
        => Create(
            seekToImportEvent,
            captureSnapshot,
            verifyProtocolAsync,
            InfraTeacher.TeacherAnnotationStore.Current);

    public static CodingProtocolImportTrainingWorkflowService Create(
        Action<CodingEvent> seekToImportEvent,
        Func<string?> captureSnapshot,
        Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>>? verifyProtocolAsync,
        ITeacherAnnotationStore annotationStore)
        => new(
            async importEvent =>
            {
                seekToImportEvent(importEvent);
                await Task.Delay(200);
            },
            captureSnapshot,
            () => CodingModeDialogServiceFactory.Create().ShowImportFrameCaptureFailed(),
            () => Guid.NewGuid().ToString("N")[..12],
            CodingProtocolTrainingSnapshotStoreFactory.Create(),
            LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation,
            annotation => annotationStore.AppendAsync(annotation),
            verifyProtocolAsync);
}
