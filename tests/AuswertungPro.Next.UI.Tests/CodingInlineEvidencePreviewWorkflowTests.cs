using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineEvidencePreviewWorkflowTests
{
    [Fact]
    public void Execute_applies_built_preview_state()
    {
        var calls = new List<string>();
        var codingEvent = new CodingEvent { Entry = new ProtocolEntry() };
        var expected = CodingInlineEvidencePreviewService.MissingImage;

        CodingInlineEvidencePreviewWorkflow.Execute(
            new CodingInlineEvidencePreviewWorkflowRequest(
                codingEvent,
                BuildPreview: ev =>
                {
                    Assert.Same(codingEvent, ev);
                    calls.Add("build");
                    return expected;
                }),
            new CodingInlineEvidencePreviewWorkflowActions(
                ApplyPreview: state =>
                {
                    Assert.Same(expected, state);
                    calls.Add("apply");
                },
                TraceError: message => calls.Add($"trace:{message}")));

        Assert.Equal(["build", "apply"], calls);
    }

    [Fact]
    public void Execute_applies_load_failed_state_and_traces_when_preview_build_fails()
    {
        var calls = new List<string>();

        CodingInlineEvidencePreviewWorkflow.Execute(
            new CodingInlineEvidencePreviewWorkflowRequest(
                new CodingEvent { Entry = new ProtocolEntry() },
                BuildPreview: _ =>
                {
                    calls.Add("build");
                    throw new InvalidOperationException("kaputt");
                }),
            new CodingInlineEvidencePreviewWorkflowActions(
                ApplyPreview: state =>
                {
                    Assert.Same(CodingInlineEvidencePreviewService.LoadFailed, state);
                    calls.Add("apply-failed");
                },
                TraceError: message => calls.Add(message)));

        Assert.Equal(
            [
                "build",
                "apply-failed",
                "[CodingPreview] kaputt"
            ],
            calls);
    }
}
