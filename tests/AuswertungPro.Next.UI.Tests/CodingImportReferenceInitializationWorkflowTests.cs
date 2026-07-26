using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportReferenceInitializationWorkflowTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Execute_skips_when_required_state_is_missing(bool hasCodingViewModel, bool hasEventCollection)
    {
        var result = CodingImportReferenceInitializationWorkflow.Execute(
            new CodingImportReferenceInitializationWorkflowRequest(hasCodingViewModel, hasEventCollection),
            ThrowingActions());

        Assert.Equal(CodingImportReferenceInitializationWorkflowOutcome.MissingRequiredState, result.Outcome);
        Assert.False(result.Initialized);
    }

    [Fact]
    public void Execute_resets_import_reference_state_in_window_order()
    {
        var calls = new List<string>();

        var result = CodingImportReferenceInitializationWorkflow.Execute(
            new CodingImportReferenceInitializationWorkflowRequest(
                HasCodingViewModel: true,
                HasEventCollection: true),
            new CodingImportReferenceInitializationWorkflowActions(
                ResetProtocolMatchState: () =>
                {
                    calls.Add("reset-match");
                    return null;
                },
                UpdateProtocolMatchSummary: routing => calls.Add($"summary:{routing is null}"),
                MoveExistingEventsToImportReference: () =>
                {
                    calls.Add("move");
                    return 3;
                },
                SetImportItemsSource: () => calls.Add("import-source"),
                SetImportCount: count => calls.Add($"import-count:{count}"),
                ClearActiveSessionEvents: () => calls.Add("clear-session"),
                SetCodingItemsSource: () => calls.Add("coding-source"),
                SetCodingCount: count => calls.Add($"coding-count:{count}"),
                BuildBaselineSignature: () =>
                {
                    calls.Add("build-baseline");
                    return "baseline";
                },
                SetBaselineSignature: signature => calls.Add($"baseline:{signature}"),
                ResetStretchTracker: () => calls.Add("stretch-reset")));

        Assert.Equal(CodingImportReferenceInitializationWorkflowOutcome.Initialized, result.Outcome);
        Assert.True(result.Initialized);
        Assert.Equal(3, result.ImportEventCount);
        Assert.Equal(
            [
                "reset-match",
                "summary:True",
                "move",
                "import-source",
                "import-count:3",
                "clear-session",
                "coding-source",
                "coding-count:0",
                "build-baseline",
                "baseline:baseline",
                "stretch-reset"
            ],
            calls);
    }

    private static CodingImportReferenceInitializationWorkflowActions ThrowingActions()
        => new(
            ResetProtocolMatchState: () => throw new InvalidOperationException("Match reset should not run."),
            UpdateProtocolMatchSummary: _ => throw new InvalidOperationException("Summary should not update."),
            MoveExistingEventsToImportReference: () => throw new InvalidOperationException("Import transfer should not run."),
            SetImportItemsSource: () => throw new InvalidOperationException("Import source should not update."),
            SetImportCount: _ => throw new InvalidOperationException("Import count should not update."),
            ClearActiveSessionEvents: () => throw new InvalidOperationException("Session events should not clear."),
            SetCodingItemsSource: () => throw new InvalidOperationException("Coding source should not update."),
            SetCodingCount: _ => throw new InvalidOperationException("Coding count should not update."),
            BuildBaselineSignature: () => throw new InvalidOperationException("Baseline should not be built."),
            SetBaselineSignature: _ => throw new InvalidOperationException("Baseline should not update."),
            ResetStretchTracker: () => throw new InvalidOperationException("Tracker should not reset."));
}
