using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerInputWorkflowTests
{
    [Fact]
    public void Key_execute_cancels_and_clears_detection_overlays_on_escape()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerKeyInputWorkflow.Execute(
            new CodingEingabemarkerKeyInputWorkflowRequest(IsEscape: true, IsEnter: false),
            KeyActions(calls));

        Assert.Equal(CodingEingabemarkerKeyInputWorkflowOutcome.Cancelled, result.Outcome);
        Assert.Equal(["cancel", "clear-detection"], calls);
    }

    [Fact]
    public void Key_execute_submits_on_enter()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerKeyInputWorkflow.Execute(
            new CodingEingabemarkerKeyInputWorkflowRequest(IsEscape: false, IsEnter: true),
            KeyActions(calls));

        Assert.Equal(CodingEingabemarkerKeyInputWorkflowOutcome.Submitted, result.Outcome);
        Assert.Equal(["submit"], calls);
    }

    [Fact]
    public void Key_execute_ignores_other_keys()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerKeyInputWorkflow.Execute(
            new CodingEingabemarkerKeyInputWorkflowRequest(IsEscape: false, IsEnter: false),
            KeyActions(calls));

        Assert.Equal(CodingEingabemarkerKeyInputWorkflowOutcome.Ignored, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Selection_execute_applies_visible_selection_and_submits()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerSelectionInputWorkflow.Execute(
            new CodingEingabemarkerSelectionInputWorkflowRequest(IsPopupVisible: true, SelectedText: "BAA"),
            SelectionActions(calls));

        Assert.Equal(CodingEingabemarkerSelectionInputWorkflowOutcome.Submitted, result.Outcome);
        Assert.Equal(["quick:BAA", "submit"], calls);
    }

    [Theory]
    [InlineData(false, "BAA", CodingEingabemarkerSelectionInputWorkflowOutcome.PopupHidden)]
    [InlineData(true, null, CodingEingabemarkerSelectionInputWorkflowOutcome.EmptySelection)]
    [InlineData(true, "", CodingEingabemarkerSelectionInputWorkflowOutcome.EmptySelection)]
    public void Selection_execute_skips_when_hidden_or_selection_is_empty(
        bool isPopupVisible,
        string? selectedText,
        CodingEingabemarkerSelectionInputWorkflowOutcome expected)
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerSelectionInputWorkflow.Execute(
            new CodingEingabemarkerSelectionInputWorkflowRequest(isPopupVisible, selectedText),
            SelectionActions(calls));

        Assert.Equal(expected, result.Outcome);
        Assert.Empty(calls);
    }

    private static CodingEingabemarkerKeyInputWorkflowActions KeyActions(List<string> calls)
        => new(
            CancelMarker: () => calls.Add("cancel"),
            ClearDetectionOverlays: () => calls.Add("clear-detection"),
            Submit: () => calls.Add("submit"));

    private static CodingEingabemarkerSelectionInputWorkflowActions SelectionActions(List<string> calls)
        => new(
            ApplyQuickSelection: text => calls.Add($"quick:{text}"),
            Submit: () => calls.Add("submit"));
}
