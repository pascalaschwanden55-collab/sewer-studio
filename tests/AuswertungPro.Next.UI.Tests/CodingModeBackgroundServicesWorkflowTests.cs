using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeBackgroundServicesWorkflowTests
{
    [Fact]
    public void Execute_starts_ai_osd_timer_badge_and_suggestion_scan_in_order()
    {
        var calls = new List<string>();

        CodingModeBackgroundServicesWorkflow.Execute(
            new CodingModeBackgroundServicesWorkflowActions(
                StartCodingAiInitialization: () => calls.Add("ai"),
                StartCodingOsdTimer: () => calls.Add("timer"),
                ShowInitialOsdMeterBadge: () => calls.Add("badge"),
                // Zuletzt: Der Vorabdurchlauf wartet intern die KI-Bereitschaft ab.
                StartSuggestionScan: () => calls.Add("vorschlaege")));

        Assert.Equal(["ai", "timer", "badge", "vorschlaege"], calls);
    }
}
