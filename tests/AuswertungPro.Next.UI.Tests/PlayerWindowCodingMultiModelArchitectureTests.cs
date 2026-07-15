using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingMultiModelArchitectureTests
{
    [Fact]
    public void PlayerWindow_multi_model_analysis_sequence_lives_in_command_workflow()
    {
        var multiModelPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.MultiModel.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelAnalysisCommandWorkflow.cs");

        Assert.True(File.Exists(commandWorkflowPath), "Multi-Model-Analyse-Sequenz muss ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var multiModel = File.ReadAllText(multiModelPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync", multiModel);
        Assert.Contains("CodingMultiModelRuntimeGateWorkflow.Execute", commandWorkflow);
        Assert.Contains("start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready", commandWorkflow);
        Assert.Contains("actions.RunInferenceAsync", commandWorkflow);

        var offenders = FindFileTokenOffenders(
                multiModelPath,
                "if (!runtimeGate.Ready)",
                "if (start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready)")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs"),
                "private void AddMultiModelFindingsAsEvents"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs"),
                "if (!_codingSessionHost.HasViewModel || codingSessionService == null) return",
                "double meter = ResolveCodingMeterForFrame"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-MultiModel-Partials sollen Command-Sequenz, Session-Guards und Meter-Aufloesung an Workflows delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_multi_model_ai_events_live_in_multimodel_partial()
    {
        var aiEventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
        var multiModelPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelFindingEventCommandWorkflow.cs");
        var addDecisionPath = RepoFile("src", "AuswertungPro.Next.Application", "Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");

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
        Assert.Contains("private void AddMultiModelFindingsAsEvents", multiModel);
        Assert.Contains("CodingMultiModelFindingEventCommandWorkflow.Execute", multiModel);
        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", multiModel);
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

        var offenders = FindFileTokenOffenders(
            multiModelPath,
            "CodingSegmentedFindingFrameMapper.Build",
            "new LiveFrameFinding(",
            "QuantificationSeverityPolicy.Estimate(",
            "dino.X1 / imageWidth",
            "CodingMultiModelFindingAddDecisionPolicy.Decide",
            "CodingFindingCoveragePolicy.FindCoveringEvent",
            "CodingFindingCoveragePolicy.IsCovered(e, meter, pseudoFinding)",
            "CodingMultiModelQualityGatePolicy.Evaluate",
            "new EvidenceVector(",
            "new QualityGateResult(dinoConf");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-MultiModel-Event-Partial soll Projektion, Coverage und QualityGate-Details an Mapper/Policies delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_segmented_finding_projection_lives_in_mapper()
    {
        var eventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var mapperPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSegmentedFindingFrameMapper.cs");

        Assert.True(File.Exists(mapperPath), "SegmentedFinding-zu-LiveFrameFinding-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.Contains("CodingSegmentedFindingFrameMapper.Build", workflow);
        Assert.Contains("public static LiveFrameFinding Build", mapper);
        Assert.Contains("VsaCodeResolver.NormalizeClock", mapper);
    }

    [Fact]
    public void PlayerWindow_multi_model_coverage_uses_existing_policy()
    {
        var eventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var decisionPath = RepoFile("src", "AuswertungPro.Next.Application", "Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var decision = File.ReadAllText(decisionPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", workflow);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", decision);
    }

    [Fact]
    public void PlayerWindow_multi_model_quality_gate_uses_policy()
    {
        var eventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelQualityGatePolicy.cs");

        Assert.True(File.Exists(policyPath), "Multi-Model-QualityGate-Evidenz muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.Contains("CodingMultiModelQualityGatePolicy.Evaluate", workflow);
        Assert.Contains("public static QualityGateResult Evaluate", policy);
        Assert.Contains("YoloConf: yoloMaxConfidence", policy);
        Assert.Contains("PlausibilityScore: officialLabel != null ? 0.8 : 0.4", policy);
    }

    [Fact]
    public void PlayerWindow_multi_model_mask_render_candidates_live_in_visibility_policy()
    {
        var renderingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Rendering.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSegmentedFindingVisibility.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelResultsRenderWorkflow.cs");

        var rendering = File.ReadAllText(renderingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", rendering);
        Assert.Contains("actions.BuildVisibleMaskRenderCandidates(request.Segmented)", workflow);
        Assert.Contains("public static IReadOnlyList<SamMaskRenderer.MaskRenderCandidate> BuildVisibleMaskRenderCandidates", policy);
    }

    [Fact]
    public void PlayerWindow_multi_model_rendering_lives_in_rendering_partial()
    {
        var renderingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Rendering.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSamMaskOverlayController.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelResultsRenderWorkflow.cs");
        var stateControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingOverlayRenderStateController.cs");

        Assert.True(File.Exists(renderingPath), "Multi-Model-Maskenanzeige soll aus dem allgemeinen Coding.Ai-Partial heraus.");
        Assert.True(File.Exists(controllerPath), "SAM-Maskenrendering soll ausserhalb von PlayerWindow verdrahtet werden.");
        Assert.True(File.Exists(workflowPath), "Multi-Model-Render-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(stateControllerPath), "Overlay-Render-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");

        var rendering = File.ReadAllText(renderingPath);
        var state = File.ReadAllText(statePath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var workflow = File.ReadAllText(workflowPath);
        var stateController = File.Exists(stateControllerPath) ? File.ReadAllText(stateControllerPath) : "";

        Assert.Contains("private void ShowMultiModelResults", rendering);
        Assert.Contains("CodingMultiModelResultsRenderWorkflow.Execute", rendering);
        Assert.Contains("CodingSamMaskOverlayController.RenderCandidates", rendering);
        Assert.Contains("_codingOverlayRenderState.SetVideoAspect", rendering);
        Assert.Contains("_codingOverlayRenderState.ShowReferenceDiameter", rendering);
        Assert.Contains("_codingOverlayRenderState", state);
        Assert.Contains("SamMaskRenderer.RenderCandidates", controller);
        Assert.Contains("public sealed class CodingOverlayRenderStateController", stateController);
        Assert.Contains("public double VideoAspect", stateController);
        Assert.Contains("public bool ShowReferenceDn", stateController);
        Assert.Contains("RenderReferenceDn", rendering);
        Assert.Contains("actions.ClearMasks()", workflow);
        Assert.Contains("actions.SetVideoAspect", workflow);
        Assert.Contains("actions.RenderCandidates", workflow);
        Assert.Contains("actions.ShowReferenceDn()", workflow);

        var offenders = FindFileTokenOffenders(
                renderingPath,
                "new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate",
                "if (mmResult.SamResponse != null)",
                "var candidates = CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates",
                "_codingVideoAspect = (double)srAsp.ImageWidth / srAsp.ImageHeight",
                "_codingVideoAspect",
                "_showReferenceDn",
                "SamMaskRenderer.RenderCandidates")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs"),
                "private void ShowMultiModelResults"))
            .Concat(FindFileTokenOffenders(
                statePath,
                "_codingVideoAspect",
                "_showReferenceDn"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-MultiModel-Rendering soll Masken-Sichtbarkeit, Render-State und SAM-Details ueber Workflows/Controller kapseln:\n"
            + string.Join("\n", offenders));
    }
}
