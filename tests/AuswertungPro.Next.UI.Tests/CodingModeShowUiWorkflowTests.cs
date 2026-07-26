using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeShowUiWorkflowTests
{
    [Fact]
    public void Execute_shows_surface_before_viewport_cursor_and_loaded_update()
    {
        var calls = new List<string>();

        CodingModeShowUiWorkflow.Execute(
            new CodingModeShowUiWorkflowActions(
                ShowCodingSurface: () => calls.Add("show"),
                UpdateCodingOverlayViewport: () => calls.Add("viewport"),
                UpdateCodingOverlayCursor: () => calls.Add("cursor"),
                ScheduleLoadedViewportUpdate: () => calls.Add("schedule")));

        Assert.Equal(["show", "viewport", "cursor", "schedule"], calls);
    }
}
