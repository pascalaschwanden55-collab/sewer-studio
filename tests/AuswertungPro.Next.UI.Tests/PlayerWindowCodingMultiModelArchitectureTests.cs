using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

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

    [Fact]
    public void PlayerWindow_segmented_finding_projection_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingFrameMapper.cs");

        Assert.True(File.Exists(mapperPath), "SegmentedFinding-zu-LiveFrameFinding-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.DoesNotContain("CodingSegmentedFindingFrameMapper.Build", events);
        Assert.DoesNotContain("new LiveFrameFinding(", events);
        Assert.DoesNotContain("QuantificationSeverityPolicy.Estimate(", events);
        Assert.DoesNotContain("dino.X1 / imageWidth", events);
        Assert.Contains("CodingSegmentedFindingFrameMapper.Build", workflow);
        Assert.Contains("public static LiveFrameFinding Build", mapper);
        Assert.Contains("VsaCodeResolver.NormalizeClock", mapper);
    }

    [Fact]
    public void PlayerWindow_multi_model_coverage_uses_existing_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var decisionPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var decision = File.ReadAllText(decisionPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.DoesNotContain("CodingMultiModelFindingAddDecisionPolicy.Decide", events);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", events);
        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", workflow);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", decision);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.IsCovered(e, meter, pseudoFinding)", events);
    }

    [Fact]
    public void PlayerWindow_multi_model_quality_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelQualityGatePolicy.cs");

        Assert.True(File.Exists(policyPath), "Multi-Model-QualityGate-Evidenz muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.DoesNotContain("CodingMultiModelQualityGatePolicy.Evaluate", events);
        Assert.DoesNotContain("new EvidenceVector(", events);
        Assert.DoesNotContain("new QualityGateResult(dinoConf", events);
        Assert.Contains("CodingMultiModelQualityGatePolicy.Evaluate", workflow);
        Assert.Contains("public static QualityGateResult Evaluate", policy);
        Assert.Contains("YoloConf: yoloMaxConfidence", policy);
        Assert.Contains("PlausibilityScore: officialLabel != null ? 0.8 : 0.4", policy);
    }

    [Fact]
    public void PlayerWindow_multi_model_mask_render_candidates_live_in_visibility_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var renderingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Rendering.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingVisibility.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelResultsRenderWorkflow.cs");

        var rendering = File.ReadAllText(renderingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", rendering);
        Assert.Contains("actions.BuildVisibleMaskRenderCandidates(request.Segmented)", workflow);
        Assert.DoesNotContain("new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate", rendering);
        Assert.Contains("public static IReadOnlyList<SamMaskRenderer.MaskRenderCandidate> BuildVisibleMaskRenderCandidates", policy);
    }

    [Fact]
    public void PlayerWindow_multi_model_rendering_lives_in_rendering_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var renderingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Rendering.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingSamMaskOverlayController.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelResultsRenderWorkflow.cs");
        var stateControllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderStateController.cs");

        Assert.True(File.Exists(renderingPath), "Multi-Model-Maskenanzeige soll aus dem allgemeinen Coding.Ai-Partial heraus.");
        Assert.True(File.Exists(controllerPath), "SAM-Maskenrendering soll ausserhalb von PlayerWindow verdrahtet werden.");
        Assert.True(File.Exists(workflowPath), "Multi-Model-Render-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(stateControllerPath), "Overlay-Render-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var rendering = File.ReadAllText(renderingPath);
        var state = File.ReadAllText(statePath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var workflow = File.ReadAllText(workflowPath);
        var stateController = File.Exists(stateControllerPath) ? File.ReadAllText(stateControllerPath) : "";

        Assert.DoesNotContain("private void ShowMultiModelResults", ai);
        Assert.Contains("private void ShowMultiModelResults", rendering);
        Assert.Contains("CodingMultiModelResultsRenderWorkflow.Execute", rendering);
        Assert.Contains("CodingSamMaskOverlayController.RenderCandidates", rendering);
        Assert.Contains("_codingOverlayRenderState.SetVideoAspect", rendering);
        Assert.Contains("_codingOverlayRenderState.ShowReferenceDiameter", rendering);
        Assert.Contains("_codingOverlayRenderState", state);
        Assert.DoesNotContain("if (mmResult.SamResponse != null)", rendering);
        Assert.DoesNotContain("var candidates = CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", rendering);
        Assert.DoesNotContain("_codingVideoAspect = (double)srAsp.ImageWidth / srAsp.ImageHeight", rendering);
        Assert.DoesNotContain("_codingVideoAspect", rendering + state);
        Assert.DoesNotContain("_showReferenceDn", rendering + state);
        Assert.DoesNotContain("SamMaskRenderer.RenderCandidates", rendering);
        Assert.Contains("SamMaskRenderer.RenderCandidates", controller);
        Assert.Contains("public sealed class CodingOverlayRenderStateController", stateController);
        Assert.Contains("public double VideoAspect", stateController);
        Assert.Contains("public bool ShowReferenceDn", stateController);
        Assert.Contains("RenderReferenceDn", rendering);
        Assert.Contains("actions.ClearMasks()", workflow);
        Assert.Contains("actions.SetVideoAspect", workflow);
        Assert.Contains("actions.RenderCandidates", workflow);
        Assert.Contains("actions.ShowReferenceDn()", workflow);
    }
}
