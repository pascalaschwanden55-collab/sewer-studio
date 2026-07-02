using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingAiEventsArchitectureTests
{
    [Fact]
    public void PlayerWindow_live_ai_events_live_in_live_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Live.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingEventWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingEventCommandWorkflow.cs");
        var overlayWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCurrentOverlayRenderWorkflow.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingSessionAppender.cs");
        var confirmationTrackerPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingConfirmationTracker.cs");
        var addDecisionPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingAddDecisionPolicy.cs");

        Assert.True(File.Exists(livePath), "Live/Qwen-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Live/Qwen-Event-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Live/Qwen-Event-Befehl soll die Fenster-Guards ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(overlayWorkflowPath), "CurrentOverlay-Render-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(appenderPath), "Live/Qwen-Event-Anwendung auf die Session soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(confirmationTrackerPath), "Live/Qwen-Bestaetigungsauswahl soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(addDecisionPath), "Live/Qwen-Add-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var live = File.ReadAllText(livePath);
        var workflow = File.ReadAllText(workflowPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var overlayWorkflow = File.Exists(overlayWorkflowPath) ? File.ReadAllText(overlayWorkflowPath) : "";
        var appender = File.ReadAllText(appenderPath);
        var confirmationTracker = File.ReadAllText(confirmationTrackerPath);
        var addDecision = File.ReadAllText(addDecisionPath);

        Assert.DoesNotContain("private void AddAiFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddAiFindingsAsEvents", live);
        Assert.Contains("CodingLiveFindingEventCommandWorkflow.Execute", live);
        Assert.Contains("CodingLiveFindingEventWorkflow.Execute", live);
        Assert.Contains("CodingCurrentOverlayRenderWorkflow.Execute", live);
        Assert.Contains("_codingSessionHost", live);
        Assert.DoesNotContain("_codingVm", live);
        Assert.DoesNotContain("_codingSessionHost.CurrentOverlay != null", live);
        Assert.DoesNotContain("if (overlay != null)", live);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || codingSessionService == null) return", live);
        Assert.DoesNotContain("double meter = ResolveCodingMeterForFrame", live);
        Assert.DoesNotContain("CodingLiveFindingEventFactory.Create", live);
        Assert.DoesNotContain("CodingLiveFindingQualityGatePolicy.Evaluate", live);
        Assert.DoesNotContain("CodingLiveFindingSessionAppender.Append", live);
        Assert.DoesNotContain("CodingLiveFindingConfirmationTracker", live);
        Assert.DoesNotContain("CodingLiveFindingAddDecisionPolicy.Decide", live);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", live);
        Assert.DoesNotContain("codingSessionService.AddEvent(entry)", live);
        Assert.DoesNotContain("codingEvent.AiContext = draft.AiContext", live);
        Assert.DoesNotContain("CodingLiveFindingAcceptancePolicy.NeedsConfirmation", live);
        Assert.DoesNotContain("CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead", live);
        Assert.DoesNotContain("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", live);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", live);
        Assert.Contains("public static class CodingLiveFindingEventWorkflow", workflow);
        Assert.Contains("actions.ResolveMeterForFrame", commandWorkflow);
        Assert.Contains("actions.ExecuteFindingWorkflow", commandWorkflow);
        Assert.Contains("request.CurrentOverlay is null", overlayWorkflow);
        Assert.Contains("actions.RenderOverlay(request.CurrentOverlay)", overlayWorkflow);
        Assert.Contains("CodingLiveFindingEventFactory.Create", workflow);
        Assert.Contains("CodingLiveFindingQualityGatePolicy.Evaluate", workflow);
        Assert.Contains("CodingLiveFindingSessionAppender.Append", workflow);
        Assert.Contains("CodingLiveFindingConfirmationTracker", workflow);
        Assert.Contains("CodingLiveFindingAddDecisionPolicy.Decide", workflow);
        Assert.Contains("public static class CodingLiveFindingSessionAppender", appender);
        Assert.Contains("attachAnalyzedFramePhoto(draft.Entry)", appender);
        Assert.Contains("addEvent(draft.Entry)", appender);
        Assert.Contains("codingEvent.AiContext = draft.AiContext", appender);
        Assert.Contains("public sealed class CodingLiveFindingConfirmationTracker", confirmationTracker);
        Assert.Contains("CodingLiveFindingAcceptancePolicy.NeedsConfirmation", confirmationTracker);
        Assert.Contains("public static CodingLiveFindingAddDecision Decide", addDecision);
        Assert.Contains("CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead", addDecision);
        Assert.Contains("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", addDecision);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", addDecision);
    }

    [Fact]
    public void PlayerWindow_coding_ai_finding_filtering_lives_in_filtering_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var resultWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiResultWorkflow.cs");
        var filteringPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Filtering.cs");
        var meterPolicyPath = Path.Combine(uiRoot, "Ai", "CodingResultMeterReadingPolicy.cs");
        var osdStateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterStateWorkflow.cs");
        var warmupPolicyPath = Path.Combine(uiRoot, "Ai", "CodingWarmupResultBufferPolicy.cs");
        var frameReadinessControllerPath = Path.Combine(uiRoot, "Player", "CodingFrameReadinessController.cs");
        var overlaySelectorPath = Path.Combine(uiRoot, "Ai", "CodingNewFindingOverlaySelector.cs");
        var findingsControlsPath = Path.Combine(windowsRoot, "CodingFindingsListControls.cs");

        Assert.True(File.Exists(filteringPath), "KI-Finding-Filteradapter sollen aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(resultWorkflowPath), "Coding-AI-Result-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(meterPolicyPath), "OSD-Meteruebernahme aus KI-Ergebnissen muss ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(osdStateWorkflowPath), "OSD-Meteruebernahme soll als State-Workflow ausserhalb der PlayerWindow-Partials angewendet werden.");
        Assert.True(File.Exists(warmupPolicyPath), "Warmup-Puffer-Auswahl muss ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(frameReadinessControllerPath), "FrameReadiness- und Warmup-Pufferzustand soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(overlaySelectorPath), "Auswahl neuer Overlay-Findings muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(findingsControlsPath), "Coding-Findings-Listenzuweisung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var resultWorkflow = File.ReadAllText(resultWorkflowPath);
        var filtering = File.ReadAllText(filteringPath);
        var meterPolicy = File.ReadAllText(meterPolicyPath);
        var osdStateWorkflow = File.ReadAllText(osdStateWorkflowPath);
        var warmupPolicy = File.ReadAllText(warmupPolicyPath);
        var frameReadinessController = File.ReadAllText(frameReadinessControllerPath);
        var overlaySelector = File.ReadAllText(overlaySelectorPath);
        var findingsControls = File.ReadAllText(findingsControlsPath);

        Assert.DoesNotContain("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", aiEvents);
        Assert.DoesNotContain("private static string? LookupVsaLabel", aiEvents);
        Assert.DoesNotContain("private string? ResolveFindingCodeForCoding", aiEvents);
        Assert.DoesNotContain("private bool IsFindingAlreadyKnown", aiEvents);
        Assert.DoesNotContain("new AiFindingDisplayItem", aiEvents);
        Assert.DoesNotContain("CodingFindingsList.ItemsSource", aiEvents);
        Assert.DoesNotContain("MeterReading.Value <= 500", aiEvents);
        Assert.DoesNotContain("MeterReading.HasValue &&", aiEvents);
        Assert.DoesNotContain("CodingResultMeterReadingPolicy.TryAccept", aiEvents);
        Assert.Contains("CodingAiResultWorkflow.Execute", aiEvents);
        Assert.DoesNotContain("CodingOsdMeterStateWorkflow.FromDetectionResult(result)", aiEvents);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromDetectionResult", aiEvents);
        Assert.Contains("ResolveOsdMeterState", resultWorkflow);
        Assert.Contains("CodingResultMeterReadingPolicy.TryAccept", osdStateWorkflow);
        Assert.DoesNotContain("var buffered = _pendingWarmupResult", aiEvents);
        Assert.DoesNotContain("buffered.Findings.Count", aiEvents);
        Assert.Contains("_codingFrameReadinessController.SelectReadyResult", aiEvents);
        Assert.Contains("SelectReadyResult", resultWorkflow);
        Assert.Contains("CodingWarmupResultBufferPolicy.Select", frameReadinessController);
        Assert.DoesNotContain("validFindings.Where(f => !IsFindingAlreadyKnown", aiEvents);
        Assert.Contains("CodingNewFindingOverlaySelector.Select", aiEvents);
        Assert.Contains("SelectFindingsToDraw", resultWorkflow);
        Assert.Contains("CodingFindingsListControls.ShowFindings(CodingFindingsList, findings)", aiEvents);
        Assert.Contains("ShowFindings", resultWorkflow);
        Assert.Contains("AiFindingDisplayItemFactory.ForFindings", findingsControls);
        Assert.Contains("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", filtering);
        Assert.Contains("private static string? LookupVsaLabel", filtering);
        Assert.Contains("private string? ResolveFindingCodeForCoding", filtering);
        Assert.Contains("private bool IsFindingAlreadyKnown", filtering);
        Assert.Contains("CodingFindingFilterPolicy.FilterValid", filtering);
        Assert.Contains("CodingFindingCodeResolver.Resolve", filtering);
        Assert.Contains("CodingKnownFindingPolicy.IsKnown", filtering);
        Assert.Contains("_codingSessionHost", filtering);
        Assert.DoesNotContain("_codingVm", filtering);
        Assert.Contains("public static bool TryAccept", meterPolicy);
        Assert.Contains("public static CodingWarmupResultSelection Select", warmupPolicy);
        Assert.Contains("public static IReadOnlyList<LiveFrameFinding> Select", overlaySelector);
    }

    [Fact]
    public void PlayerWindow_ai_event_partials_read_session_state_through_session_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Live.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.MultiModel.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Boundary.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Structural.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Streckenschaden.cs")
        };

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} muss als PlayerWindow-Partial existieren.");
            var text = File.ReadAllText(path);
            Assert.Contains("_codingSessionHost", text);
            Assert.DoesNotContain("_codingVm", text);
        }
    }
}
