using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeBackgroundServicesWorkflowTests
{
    [Fact]
    public void Execute_starts_ai_osd_timer_and_initial_badge_in_order()
    {
        var calls = new List<string>();

        CodingModeBackgroundServicesWorkflow.Execute(
            new CodingModeBackgroundServicesWorkflowActions(
                StartCodingAiInitialization: () => calls.Add("ai"),
                StartCodingOsdTimer: () => calls.Add("timer"),
                ShowInitialOsdMeterBadge: () => calls.Add("badge")));

        Assert.Equal(["ai", "timer", "badge"], calls);
    }
}
