using AuswertungPro.Next.Domain.Models;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolImportTrainingWorkflowServiceFactory
{
    public static CodingProtocolImportTrainingWorkflowService Create(
        Action<CodingEvent> seekToImportEvent,
        Func<string?> captureSnapshot)
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
            annotation => InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation));
}
