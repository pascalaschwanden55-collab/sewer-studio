using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolImportTrainingConfirmationWorkflowTests
{
    [Fact]
    public async Task ConfirmAsync_creates_training_service_and_delegates_confirmation()
    {
        var calls = new List<string>();
        var importEvent = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BAG" },
            MeterAtCapture = 12.34
        };
        Action<CodingEvent> seekToImportEvent = actualEvent =>
        {
            Assert.Same(importEvent, actualEvent);
            calls.Add("seek");
        };
        Func<string?> captureSnapshot = () =>
        {
            calls.Add("capture");
            return @"C:\temp\snap.png";
        };
        var snapshotStore = new CodingProtocolTrainingSnapshotStore(
            () => @"C:\teacher\images",
            path => path == @"C:\temp\snap.png",
            (_, _, _) => calls.Add("copy"),
            _ => calls.Add("delete"));

        var result = await CodingProtocolImportTrainingConfirmationWorkflow.ConfirmAsync(
            importEvent,
            seekToImportEvent,
            captureSnapshot,
            new CodingProtocolImportTrainingConfirmationWorkflowActions(
                CreateService: (seek, capture) =>
                {
                    Assert.Same(seekToImportEvent, seek);
                    Assert.Same(captureSnapshot, capture);
                    calls.Add("service");
                    return new CodingProtocolImportTrainingWorkflowService(
                        seekAndWait: actualEvent =>
                        {
                            seek(actualEvent);
                            return Task.CompletedTask;
                        },
                        captureSnapshot: capture,
                        showFrameCaptureFailed: () => throw new InvalidOperationException("Failure dialog must not be shown."),
                        createAnnotationId: () => "abc123",
                        snapshotStore,
                        createAnnotation: LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation,
                        appendAnnotation: _ =>
                        {
                            calls.Add("append");
                            return Task.CompletedTask;
                        });
                }));

        Assert.True(result.Accepted);
        Assert.Equal("? BAG @ 12.3m bestaetigt", result.Badge.Text);
        Assert.Equal(["service", "seek", "capture", "copy", "append", "delete"], calls);
    }
}
