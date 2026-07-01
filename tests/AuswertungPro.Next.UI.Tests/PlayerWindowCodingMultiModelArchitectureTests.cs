using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingMultiModelArchitectureTests
{
    [Fact]
    public void PlayerWindow_multi_model_analysis_sequence_lives_in_command_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var multiModelPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.MultiModel.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelAnalysisCommandWorkflow.cs");

        Assert.True(File.Exists(commandWorkflowPath), "Multi-Model-Analyse-Sequenz muss ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var multiModel = File.ReadAllText(multiModelPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync", multiModel);
        Assert.DoesNotContain("if (!runtimeGate.Ready)", multiModel);
        Assert.DoesNotContain("if (start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready)", multiModel);
        Assert.Contains("CodingMultiModelRuntimeGateWorkflow.Execute", commandWorkflow);
        Assert.Contains("start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready", commandWorkflow);
        Assert.Contains("actions.RunInferenceAsync", commandWorkflow);
    }

    [Fact]
    public void PlayerWindow_multi_model_ai_events_live_in_multimodel_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var multiModelPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventCommandWorkflow.cs");
        var addDecisionPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");

        Assert.True(File.Exists(multiModelPath), "Multi-Model-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Multi-Model-Event-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Multi-Model-Event-Befehl soll die Fenster-Guards ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(addDecisionPath), "Multi-Model-Add-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var multiModel = File.ReadAllText(multiModelPath);
        var workflow = File.ReadAllText(workflowPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var addDecision = File.ReadAllText(addDecisionPath);

        Assert.Contains("_codingSessionHost", aiEvents);
        Assert.DoesNotContain("_codingVm", aiEvents);
        Assert.DoesNotContain("private void AddMultiModelFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddMultiModelFindingsAsEvents", multiModel);
        Assert.Contains("CodingMultiModelFindingEventCommandWorkflow.Execute", multiModel);
        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", multiModel);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || codingSessionService == null) return", multiModel);
        Assert.DoesNotContain("double meter = ResolveCodingMeterForFrame", multiModel);
        Assert.DoesNotContain("CodingSegmentedFindingFrameMapper.Build", multiModel);
        Assert.DoesNotContain("CodingMultiModelQualityGatePolicy.Evaluate", multiModel);
        Assert.DoesNotContain("CodingMultiModelFindingAddDecisionPolicy.Decide", multiModel);
        Assert.DoesNotContain("CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser", multiModel);
        Assert.DoesNotContain("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", multiModel);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", multiModel);
        Assert.Contains("actions.ResolveMeterForFrame", commandWorkflow);
        Assert.Contains("actions.ApplyStretchTracking", commandWorkflow);
        Assert.Contains("actions.ExecuteFindingWorkflow", commandWorkflow);
        Assert.Contains("public static class CodingMultiModelFindingEventWorkflow", workflow);
        Assert.Contains("CodingSegmentedFindingFrameMapper.Build", workflow);
        Assert.Contains("CodingMultiModelQualityGatePolicy.Evaluate", workflow);
        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", workflow);
        Assert.Contains("public static CodingMultiModelFindingAddDecision Decide", addDecision);
        Assert.Contains("CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser", addDecision);
        Assert.Contains("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", addDecision);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", addDecision);
    }
}
