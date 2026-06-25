using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerDetectionShortcutWorkflowTests
{
    [Fact]
    public void Execute_toggles_coding_live_ai_in_coding_mode()
    {
        var calls = new List<string>();

        var result = PlayerDetectionShortcutWorkflow.Execute(
            new PlayerDetectionShortcutWorkflowRequest(
                IsCodingMode: true,
                IsCodingLiveAiChecked: false,
                IsLiveDetectionChecked: false),
            Actions(calls));

        Assert.Equal(["coding:True", "invoke-coding"], calls);
        Assert.Equal(PlayerDetectionShortcutWorkflowOutcome.CodingLiveAiToggled, result.Outcome);
    }

    [Fact]
    public void Execute_toggles_live_detection_outside_coding_mode()
    {
        var calls = new List<string>();

        var result = PlayerDetectionShortcutWorkflow.Execute(
            new PlayerDetectionShortcutWorkflowRequest(
                IsCodingMode: false,
                IsCodingLiveAiChecked: false,
                IsLiveDetectionChecked: true),
            Actions(calls));

        Assert.Equal(["live:False", "invoke-live"], calls);
        Assert.Equal(PlayerDetectionShortcutWorkflowOutcome.LiveDetectionToggled, result.Outcome);
    }

    private static PlayerDetectionShortcutWorkflowActions Actions(List<string> calls)
        => new(
            SetCodingLiveAiChecked: value => calls.Add($"coding:{value}"),
            InvokeCodingLiveAi: () => calls.Add("invoke-coding"),
            SetLiveDetectionChecked: value => calls.Add($"live:{value}"),
            InvokeLiveDetection: () => calls.Add("invoke-live"));
}
