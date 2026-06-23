using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolImportTrainingWorkflowServiceTests
{
    [Fact]
    public async Task ConfirmAsync_seeks_captures_copies_appends_deletes_snapshot_and_returns_badge()
    {
        var importEvent = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BAG", Beschreibung = "Versatz" },
            MeterAtCapture = 12.34,
            VideoTimestamp = TimeSpan.FromSeconds(8)
        };
        var seeked = false;
        var deleted = false;
        TeacherAnnotation? appended = null;
        var store = new CodingProtocolTrainingSnapshotStore(
            () => @"C:\teacher\images",
            path => path == @"C:\temp\snap.png",
            (source, destination, overwrite) =>
            {
                Assert.Equal(@"C:\temp\snap.png", source);
                Assert.Equal(@"C:\teacher\images\mark_abc123.png", destination);
                Assert.True(overwrite);
            },
            path =>
            {
                Assert.Equal(@"C:\temp\snap.png", path);
                deleted = true;
            });
        var service = new CodingProtocolImportTrainingWorkflowService(
            seekAndWait: ev =>
            {
                Assert.Same(importEvent, ev);
                seeked = true;
                return Task.CompletedTask;
            },
            captureSnapshot: () => @"C:\temp\snap.png",
            showFrameCaptureFailed: () => throw new InvalidOperationException("Failure dialog must not be shown."),
            createAnnotationId: () => "abc123",
            snapshotStore: store,
            createAnnotation: LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation,
            appendAnnotation: annotation =>
            {
                appended = annotation;
                return Task.CompletedTask;
            });

        var result = await service.ConfirmAsync(importEvent);

        Assert.True(result.Accepted);
        Assert.True(seeked);
        Assert.True(deleted);
        Assert.NotNull(appended);
        Assert.Equal("abc123", appended.AnnotationId);
        Assert.Equal("BAG", appended.VsaCode);
        Assert.Equal(@"C:\teacher\images\mark_abc123.png", appended.FullFramePath);
        Assert.Equal("? BAG @ 12.3m bestaetigt", result.Badge.Text);
        Assert.Equal(TimeSpan.FromSeconds(3), result.Badge.AutoHideDelay);
    }

    [Fact]
    public async Task ConfirmAsync_shows_failure_and_returns_not_accepted_when_snapshot_is_missing()
    {
        var failureShown = false;
        var service = new CodingProtocolImportTrainingWorkflowService(
            seekAndWait: _ => Task.CompletedTask,
            captureSnapshot: () => null,
            showFrameCaptureFailed: () => failureShown = true,
            createAnnotationId: () => throw new InvalidOperationException("Annotation id must not be created."),
            snapshotStore: new CodingProtocolTrainingSnapshotStore(
                () => @"C:\teacher\images",
                _ => false,
                (_, _, _) => throw new InvalidOperationException("Snapshot must not be copied."),
                _ => { }),
            createAnnotation: (_, _, _) => throw new InvalidOperationException("Annotation must not be created."),
            appendAnnotation: _ => throw new InvalidOperationException("Annotation must not be appended."));

        var result = await service.ConfirmAsync(new CodingEvent());

        Assert.False(result.Accepted);
        Assert.True(failureShown);
    }

    [Fact]
    public async Task ConfirmAsync_shows_failure_and_returns_not_accepted_when_snapshot_copy_fails()
    {
        var failureShown = false;
        var service = new CodingProtocolImportTrainingWorkflowService(
            seekAndWait: _ => Task.CompletedTask,
            captureSnapshot: () => @"C:\temp\snap.png",
            showFrameCaptureFailed: () => failureShown = true,
            createAnnotationId: () => "abc123",
            snapshotStore: new CodingProtocolTrainingSnapshotStore(
                () => @"C:\teacher\images",
                _ => false,
                (_, _, _) => throw new InvalidOperationException("Snapshot must not be copied."),
                _ => { }),
            createAnnotation: (_, _, _) => throw new InvalidOperationException("Annotation must not be created."),
            appendAnnotation: _ => throw new InvalidOperationException("Annotation must not be appended."));

        var result = await service.ConfirmAsync(new CodingEvent());

        Assert.False(result.Accepted);
        Assert.True(failureShown);
    }

    [Fact]
    public void Factory_creates_workflow_service()
    {
        var service = CodingProtocolImportTrainingWorkflowServiceFactory.Create(
            _ => { },
            () => null);

        Assert.NotNull(service);
    }
}
