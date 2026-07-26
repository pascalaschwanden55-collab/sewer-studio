using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveAiToggleWorkflowTests
{
    [Fact]
    public void Execute_starts_timer_and_sets_active_status_when_checked()
    {
        var calls = new List<string>();

        var result = CodingLiveAiToggleWorkflow.Execute(
            new CodingLiveAiToggleWorkflowRequest(IsChecked: true, ModelName: "qwen3-vl"),
            Actions(calls));

        Assert.Equal(CodingLiveAiToggleWorkflowOutcome.Started, result);
        Assert.Equal(["start", "status:Automatische KI-Analyse aktiv|Intervall alle 5 Sekunden | qwen3-vl"], calls);
    }

    [Fact]
    public void Execute_stops_timer_and_sets_inactive_status_when_unchecked()
    {
        var calls = new List<string>();

        var result = CodingLiveAiToggleWorkflow.Execute(
            new CodingLiveAiToggleWorkflowRequest(IsChecked: false, ModelName: "qwen3-vl"),
            Actions(calls));

        Assert.Equal(CodingLiveAiToggleWorkflowOutcome.Stopped, result);
        Assert.Equal(["stop:True", "status:Künstliche Intelligenz bereit|Modell: qwen3-vl"], calls);
    }

    private static CodingLiveAiToggleWorkflowActions Actions(List<string> calls)
        => new(
            StartTimers: () => calls.Add("start"),
            StopTimers: resetButton => calls.Add($"stop:{resetButton}"),
            SetCodingAiState: (status, _, detail) => calls.Add($"status:{status}|{detail}"));
}
