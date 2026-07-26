using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingUnappliedChangesCloseWorkflowTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Execute_allows_close_without_work_when_coding_context_is_missing(bool isCodingMode, bool hasCodingViewModel)
    {
        var result = CodingUnappliedChangesCloseWorkflow.Execute(
            new CodingUnappliedChangesCloseWorkflowRequest(
                isCodingMode,
                hasCodingViewModel,
                Events: [Event("BAA")],
                BaselineSignature: "baseline"),
            ThrowingActions());

        Assert.Equal(CodingUnappliedChangesCloseWorkflowOutcome.NoCodingContext, result.Outcome);
        Assert.True(result.ShouldClose);
    }

    [Fact]
    public void Execute_allows_close_without_prompt_when_signature_matches_baseline()
    {
        var calls = new List<string>();
        var events = new[] { Event("BAA") };

        var result = CodingUnappliedChangesCloseWorkflow.Execute(
            new CodingUnappliedChangesCloseWorkflowRequest(
                IsCodingMode: true,
                HasCodingViewModel: true,
                events,
                BaselineSignature: "same"),
            new CodingUnappliedChangesCloseWorkflowActions(
                BuildSignature: actualEvents =>
                {
                    Assert.Same(events, actualEvents);
                    calls.Add("signature");
                    return "same";
                },
                ConfirmWithSuspendedOverlay: () => throw new InvalidOperationException("Confirm should not run.")));

        Assert.Equal(CodingUnappliedChangesCloseWorkflowOutcome.NoChanges, result.Outcome);
        Assert.True(result.ShouldClose);
        Assert.Equal(["signature"], calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_prompts_with_suspended_overlay_when_signature_differs(bool confirmResult)
    {
        var calls = new List<string>();

        var result = CodingUnappliedChangesCloseWorkflow.Execute(
            new CodingUnappliedChangesCloseWorkflowRequest(
                IsCodingMode: true,
                HasCodingViewModel: true,
                Events: [Event("BAA")],
                BaselineSignature: "old"),
            new CodingUnappliedChangesCloseWorkflowActions(
                BuildSignature: _ =>
                {
                    calls.Add("signature");
                    return "new";
                },
                ConfirmWithSuspendedOverlay: () =>
                {
                    calls.Add("confirm");
                    return confirmResult;
                }));

        Assert.Equal(CodingUnappliedChangesCloseWorkflowOutcome.Prompted, result.Outcome);
        Assert.Equal(confirmResult, result.ShouldClose);
        Assert.Equal(["signature", "confirm"], calls);
    }

    private static CodingUnappliedChangesCloseWorkflowActions ThrowingActions()
        => new(
            BuildSignature: _ => throw new InvalidOperationException("Signature should not be built."),
            ConfirmWithSuspendedOverlay: () => throw new InvalidOperationException("Confirm should not run."));

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = Guid.NewGuid(),
                Code = code
            }
        };
}
