using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerInputControllerTests
{
    [Fact]
    public void HandleKey_preserves_escape_enter_and_ignore_behavior()
    {
        var calls = new List<string>();
        var controller = new CodingEingabemarkerInputController(Bindings(calls));

        var cancelled = controller.HandleKey(isEscape: true, isEnter: false);
        var submitted = controller.HandleKey(isEscape: false, isEnter: true);
        var ignored = controller.HandleKey(isEscape: false, isEnter: false);

        Assert.Equal(CodingEingabemarkerKeyInputWorkflowOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(CodingEingabemarkerKeyInputWorkflowOutcome.Submitted, submitted.Outcome);
        Assert.Equal(CodingEingabemarkerKeyInputWorkflowOutcome.Ignored, ignored.Outcome);
        Assert.Equal(["cancel", "clear", "submit"], calls);
    }

    [Fact]
    public void HandleSelection_applies_visible_selection_before_submit()
    {
        var calls = new List<string>();
        var controller = new CodingEingabemarkerInputController(Bindings(calls));

        var hidden = controller.HandleSelection(isPopupVisible: false, selectedText: "Riss");
        var empty = controller.HandleSelection(isPopupVisible: true, selectedText: null);
        var submitted = controller.HandleSelection(isPopupVisible: true, selectedText: "Riss");

        Assert.Equal(CodingEingabemarkerSelectionInputWorkflowOutcome.PopupHidden, hidden.Outcome);
        Assert.Equal(CodingEingabemarkerSelectionInputWorkflowOutcome.EmptySelection, empty.Outcome);
        Assert.Equal(CodingEingabemarkerSelectionInputWorkflowOutcome.Submitted, submitted.Outcome);
        Assert.Equal(["apply:Riss", "submit"], calls);
    }

    private static CodingEingabemarkerInputControllerBindings Bindings(List<string> calls)
        => new(
            CancelMarker: () => calls.Add("cancel"),
            ClearDetectionOverlays: () => calls.Add("clear"),
            Submit: () => calls.Add("submit"),
            ApplyQuickSelection: text => calls.Add($"apply:{text}"));
}
