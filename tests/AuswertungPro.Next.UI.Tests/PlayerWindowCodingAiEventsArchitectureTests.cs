using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingAiEventsArchitectureTests
{
    [Fact]
    public void PlayerWindow_live_ai_events_live_in_live_partial()
    {
        var aiEventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
        var livePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.Live.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveFindingEventWorkflow.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveFindingEventCommandWorkflow.cs");
        var overlayWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingCurrentOverlayRenderWorkflow.cs");
        var appenderPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveFindingSessionAppender.cs");
        var confirmationTrackerPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveFindingConfirmationTracker.cs");
        var addDecisionPath = RepoFile("src", "AuswertungPro.Next.Application", "Ai", "CodingLiveFindingAddDecisionPolicy.cs");

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

        Assert.Contains("private void AddAiFindingsAsEvents", live);
        Assert.Contains("CodingLiveFindingEventCommandWorkflow.Execute", live);
        Assert.Contains("CodingLiveFindingEventWorkflow.Execute", live);
        Assert.Contains("CodingCurrentOverlayRenderWorkflow.Execute", live);
        Assert.Contains("_codingSessionHost", live);
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
    public void PlayerWindow_coding_ai_finding_filtering_lives_in_context()
    {
        var aiEventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
        var resultWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAiResultWorkflow.cs");
        var filteringPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.Filtering.cs");
        var contextPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingFindingContext.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var meterPolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingResultMeterReadingPolicy.cs");
        var osdStateWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingOsdMeterStateWorkflow.cs");
        var warmupPolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingWarmupResultBufferPolicy.cs");
        var frameReadinessControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingFrameReadinessController.cs");
        var overlaySelectorPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingNewFindingOverlaySelector.cs");
        var findingsControlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "CodingFindingsListControls.cs");

        Assert.False(File.Exists(filteringPath), "KI-Finding-Kontext soll kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(contextPath), "KI-Finding-Filterung und -Aufloesung sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(resultWorkflowPath), "Coding-AI-Result-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(meterPolicyPath), "OSD-Meteruebernahme aus KI-Ergebnissen muss ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(osdStateWorkflowPath), "OSD-Meteruebernahme soll als State-Workflow ausserhalb der PlayerWindow-Partials angewendet werden.");
        Assert.True(File.Exists(warmupPolicyPath), "Warmup-Puffer-Auswahl muss ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(frameReadinessControllerPath), "FrameReadiness- und Warmup-Pufferzustand soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(overlaySelectorPath), "Auswahl neuer Overlay-Findings muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(findingsControlsPath), "Coding-Findings-Listenzuweisung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var resultWorkflow = File.ReadAllText(resultWorkflowPath);
        var context = File.ReadAllText(contextPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var meterPolicy = File.ReadAllText(meterPolicyPath);
        var osdStateWorkflow = File.ReadAllText(osdStateWorkflowPath);
        var warmupPolicy = File.ReadAllText(warmupPolicyPath);
        var frameReadinessController = File.ReadAllText(frameReadinessControllerPath);
        var overlaySelector = File.ReadAllText(overlaySelectorPath);
        var findingsControls = File.ReadAllText(findingsControlsPath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows"),
                    "PlayerWindow*.cs")
                .Select(File.ReadAllText));

        Assert.Contains("CodingAiResultWorkflow.Execute", aiEvents);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromDetectionResult", aiEvents);
        Assert.Contains("ResolveOsdMeterState", resultWorkflow);
        Assert.Contains("CodingResultMeterReadingPolicy.TryAccept", osdStateWorkflow);
        Assert.Contains("_codingFrameReadinessController.SelectReadyResult", aiEvents);
        Assert.Contains("SelectReadyResult", resultWorkflow);
        Assert.Contains("CodingWarmupResultBufferPolicy.Select", frameReadinessController);
        Assert.Contains("CodingNewFindingOverlaySelector.Select", aiEvents);
        Assert.Contains("SelectFindingsToDraw", resultWorkflow);
        Assert.Contains("CodingFindingsListControls.ShowFindings(CodingFindingsList, findings)", aiEvents);
        Assert.Contains("ShowFindings", resultWorkflow);
        Assert.Contains("AiFindingDisplayItemFactory.ForFindings", findingsControls);
        Assert.Contains("_codingFindingContext.FilterValid", aiEvents);
        Assert.Contains("_codingFindingContext.IsKnown", aiEvents);
        Assert.Contains("private readonly Ai.CodingFindingContext _codingFindingContext", state);
        Assert.Contains("_codingFindingContext = CodingFindingContext.CreateDefault", windowRoot);
        Assert.Contains("CodingFindingFilterPolicy.FilterValid", context);
        Assert.Contains("CodingFindingCodeResolver.Resolve", context);
        Assert.Contains("CodingKnownFindingPolicy.IsKnown", context);
        Assert.Contains("VsaCodeResolver.LookupLabel", context);
        Assert.DoesNotContain("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", playerWindowPartials);
        Assert.DoesNotContain("private static string? LookupVsaLabel", playerWindowPartials);
        Assert.DoesNotContain("private string? ResolveFindingCodeForCoding", playerWindowPartials);
        Assert.DoesNotContain("private bool IsFindingAlreadyKnown", playerWindowPartials);
        Assert.Contains("public static bool TryAccept", meterPolicy);
        Assert.Contains("public static CodingWarmupResultSelection Select", warmupPolicy);
        Assert.Contains("public static IReadOnlyList<LiveFrameFinding> Select", overlaySelector);
    }

    [Fact]
    public void PlayerWindow_ai_event_partials_read_session_state_through_session_host()
    {
        var paths = new[]
        {
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.Live.cs"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Boundary.cs"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Structural.cs")
        };

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} muss als PlayerWindow-Partial existieren.");
            var text = File.ReadAllText(path);
            Assert.Contains("_codingSessionHost", text);
        }

        var windowRoot = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"));
        var stretchController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Player", "CodingStreckenschadenTrackingController.cs"));
        Assert.Contains("new CodingStreckenschadenTrackingController(", windowRoot);
        Assert.Contains("_codingSessionHost,", windowRoot);
        Assert.Contains("private readonly ICodingSessionHost _sessionHost;", stretchController);
    }
}
