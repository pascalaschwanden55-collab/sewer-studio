using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolImportTrainingConfirmationWorkflowTests
{
    [Fact]
    public void ConfirmAsync_offers_default_training_service_wiring()
    {
        var overload = typeof(CodingProtocolImportTrainingConfirmationWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingProtocolImportTrainingConfirmationWorkflow.ConfirmAsync) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(
                    [
                        typeof(CodingEvent),
                        typeof(Action<CodingEvent>),
                        typeof(Func<string>),
                    ]));

        Assert.NotNull(overload);
    }

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
                CreateService: (seek, capture, verifyProtocolAsync) =>
                {
                    Assert.Same(seekToImportEvent, seek);
                    Assert.Same(captureSnapshot, capture);
                    Assert.Null(verifyProtocolAsync);
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

    [Fact]
    public async Task ConfirmAsync_forwards_protocol_verifier_to_training_service()
    {
        var calls = new List<string>();
        var importEvent = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BAG" },
            MeterAtCapture = 12.34
        };
        var verification = new CodingProtocolVerificationResult(
            ConfirmationLevel: "bestaetigt",
            DamageVisible: true,
            ActualCode: "BAG",
            MeterReading: 12.3,
            Explanation: "Qwen bestaetigt den Importeintrag.");
        Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>> verify =
            (framePath, actualEvent) =>
            {
                Assert.Equal(@"C:\teacher\images\mark_abc123.png", framePath);
                Assert.Same(importEvent, actualEvent);
                calls.Add("verify");
                return Task.FromResult<CodingProtocolVerificationResult?>(verification);
            };

        var result = await CodingProtocolImportTrainingConfirmationWorkflow.ConfirmAsync(
            importEvent,
            _ => calls.Add("seek"),
            () =>
            {
                calls.Add("capture");
                return @"C:\temp\snap.png";
            },
            verify,
            new CodingProtocolImportTrainingConfirmationWorkflowActions(
                CreateService: (seek, capture, verifyProtocolAsync) =>
                {
                    Assert.Same(verify, verifyProtocolAsync);
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
                        snapshotStore: new CodingProtocolTrainingSnapshotStore(
                            () => @"C:\teacher\images",
                            path => path == @"C:\temp\snap.png",
                            (_, _, _) => calls.Add("copy"),
                            _ => calls.Add("delete")),
                        createAnnotation: LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation,
                        appendAnnotation: _ =>
                        {
                            calls.Add("append");
                            return Task.CompletedTask;
                        },
                        verifyProtocolAsync: verifyProtocolAsync);
                }));

        Assert.True(result.Accepted);
        Assert.Same(verification, result.Verification);
        Assert.Equal(["service", "seek", "capture", "copy", "verify", "append", "delete"], calls);
    }
}
