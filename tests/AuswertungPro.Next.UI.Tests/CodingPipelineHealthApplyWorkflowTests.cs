using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPipelineHealthApplyWorkflowTests
{
    [Fact]
    public void Execute_enables_multimodel_before_applying_enabled_ui_state()
    {
        var calls = new List<string>();

        CodingPipelineHealthApplyWorkflow.Execute(
            new CodingPipelineHealthApplyWorkflowRequest(FullStatus()),
            Actions(calls));

        Assert.Equal(
            [
                "multi:True",
                "ensure",
                "state:bereit|alles ok",
                "analyze:True",
                "details:Modus: Multi-Model"
            ],
            calls);
    }

    [Fact]
    public void Execute_disables_multimodel_without_creating_multimodel_service()
    {
        var calls = new List<string>();

        CodingPipelineHealthApplyWorkflow.Execute(
            new CodingPipelineHealthApplyWorkflowRequest(DownStatus()),
            Actions(calls));

        Assert.Equal(
            [
                "multi:False",
                "state:aus|offline",
                "analyze:False",
                "details:Modus: KI aus"
            ],
            calls);
    }

    private static CodingPipelineHealthApplyWorkflowActions Actions(List<string> calls)
        => new(
            SetUseMultiModel: enabled => calls.Add($"multi:{enabled}"),
            EnsureMultiModel: () => calls.Add("ensure"),
            SetCodingAiState: (status, _, detail) => calls.Add($"state:{status}|{detail}"),
            SetAnalyzeButtonEnabled: enabled => calls.Add($"analyze:{enabled}"),
            UpdatePipelineHealthDetails: details => calls.Add($"details:{details.Mode}"));

    private static PipelineHealthStatus FullStatus()
        => new(
            PipelineHealthLevel.Full,
            MultiModelActive: true,
            SidecarReachable: true,
            TokenValid: true,
            SidecarHealthy: true,
            QwenAvailable: true,
            YoloLoaded: true,
            DinoLoaded: true,
            SamLoaded: true,
            Summary: "bereit",
            Detail: "alles ok");

    private static PipelineHealthStatus DownStatus()
        => new(
            PipelineHealthLevel.Down,
            MultiModelActive: false,
            SidecarReachable: false,
            TokenValid: false,
            SidecarHealthy: false,
            QwenAvailable: false,
            YoloLoaded: false,
            DinoLoaded: false,
            SamLoaded: false,
            Summary: "aus",
            Detail: "offline");
}
