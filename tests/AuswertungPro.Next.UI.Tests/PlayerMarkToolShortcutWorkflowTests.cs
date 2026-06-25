using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerMarkToolShortcutWorkflowTests
{
    [Fact]
    public void Execute_deactivates_active_mark_tool()
    {
        var calls = new List<string>();

        var result = PlayerMarkToolShortcutWorkflow.Execute(
            new PlayerMarkToolShortcutWorkflowRequest(OverlayToolType.Rectangle),
            Actions(calls));

        Assert.Equal(["deactivate"], calls);
        Assert.Equal(PlayerMarkToolShortcutWorkflowOutcome.Deactivated, result.Outcome);
    }

    [Fact]
    public void Execute_toggles_popup_without_active_mark_tool()
    {
        var calls = new List<string>();

        var result = PlayerMarkToolShortcutWorkflow.Execute(
            new PlayerMarkToolShortcutWorkflowRequest(OverlayToolType.None),
            Actions(calls));

        Assert.Equal(["toggle"], calls);
        Assert.Equal(PlayerMarkToolShortcutWorkflowOutcome.PopupToggled, result.Outcome);
    }

    private static PlayerMarkToolShortcutWorkflowActions Actions(List<string> calls)
        => new(
            DeactivateMarkTool: () => calls.Add("deactivate"),
            ToggleMarkToolPopup: () => calls.Add("toggle"));
}
