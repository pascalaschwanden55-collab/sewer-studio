using System.Collections.Generic;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowStretchDamageArchitectureTests
{
    [Fact]
    public void PlayerWindow_open_stretch_damage_prompt_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundaryContextPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryContext.cs");
        var closePromptPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Streckenschaden.ClosePrompt.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePromptBuilder.cs");
        var closePolicyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePolicy.cs");
        var closeApplierPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageCloseApplier.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogServiceFactory.cs");
        var dialogWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePromptCommandWorkflow.cs");

        Assert.True(File.Exists(closePromptPath), "Dialog fuer offene Streckenschaeden soll aus dem Boundary-Partial heraus.");
        Assert.True(File.Exists(policyPath), "Dialogtext fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closePolicyPath), "Filter- und Schliessmeterlogik fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closeApplierPath), "Schliessanwendung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Dialogentscheidung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "DialogHost-Verdrahtung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogWorkflowPath), "Dialogaufruf fuer offene Streckenschaeden soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Offene-Streckenschaden-Dialogfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var boundaryContext = File.ReadAllText(boundaryContextPath);
        var closePrompt = File.ReadAllText(closePromptPath);
        var policy = File.ReadAllText(policyPath);
        var closePolicy = File.ReadAllText(closePolicyPath);
        var closeApplier = File.ReadAllText(closeApplierPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var dialogWorkflow = File.Exists(dialogWorkflowPath) ? File.ReadAllText(dialogWorkflowPath) : "";
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);

        AssertNoForbiddenTokens(boundaryContext, "private bool CloseOpenStreckenschaeden");
        Assert.Contains("private bool CloseOpenStreckenschaeden", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePromptCommandWorkflow.Execute", closePrompt);
        Assert.Contains("CodingOpenStretchDamageDialogWorkflow.ConfirmClose", closePrompt);
        AssertNoForbiddenTokens(
            closePrompt,
            "CodingOpenStretchDamageDialogServiceFactory.Create",
            "new CodingOpenStretchDamageDialogWorkflowActions",
            "if (!_codingSessionHost.HasViewModel) return true",
            "if (offene.Count == 0) return true",
            "if (decision == CodingOpenStretchDamageDialogDecision.Close)",
            "if (decision == CodingOpenStretchDamageDialogDecision.Cancel)",
            "CodingOpenStretchDamagePolicy.ResolveCloseMeter",
            "_codingSessionService?.UpdateEvent",
            ".ConfirmClose(openEvents, closeMeter))",
            "DialogConfirm",
            "new System.Text.StringBuilder",
            "Folgende Streckensch",
            ".Where(e => e.Entry.IsStreckenschaden",
            "ev.MeterAtCapture > start");
        Assert.Contains("CodingOpenStretchDamagePolicy.FindOpen", closePrompt);
        Assert.Contains("CodingOpenStretchDamageCloseApplier.Apply", closePrompt);
        Assert.Contains("_codingSessionHost", closePrompt);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("public static IReadOnlyList<CodingEvent> FindOpen", closePolicy);
        Assert.Contains("CodingOpenStretchDamagePolicy.ResolveCloseMeter", closeApplier);
        Assert.Contains("codingSessionService?.UpdateEvent", closeApplier);
        Assert.Contains("CodingOpenStretchDamagePromptBuilder.Build", dialogService);
        Assert.Contains("CodingOpenStretchDamageDialogDecision", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("ConfirmCancel", dialogServiceFactory);
        Assert.Contains("CodingOpenStretchDamageDialogServiceFactory.Create", dialogWorkflow);
        Assert.Contains("new CodingOpenStretchDamageDialogWorkflowActions", dialogWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlay", dialogWorkflow);
        Assert.Contains("service.ConfirmClose(openEvents, closeMeter)", dialogWorkflow);
        Assert.Contains("actions.FindOpen", commandWorkflow);
        Assert.Contains("actions.ConfirmClose", commandWorkflow);
        Assert.Contains("actions.ApplyClose", commandWorkflow);
        Assert.Contains("actions.RefreshEvents()", commandWorkflow);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_action_input_lives_in_builder()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingStreckenschadenTrackingController.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionInputBuilder.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplier.cs");

        Assert.True(File.Exists(builderPath), "Mapper-Eingabe fuer Streckenschaden-Aktionen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Streckenschaden-Aktionsausfuehrung muss den Action-Input-Builder nutzen.");

        var playerWindowSource = ReadPlayerWindowSource(uiRoot);
        var controller = File.ReadAllText(controllerPath);
        var builder = File.ReadAllText(builderPath);
        var applier = File.ReadAllText(applierPath);

        Assert.Contains("CodingStreckenschadenActionInputBuilder.BuildOpenEntries", applier);
        AssertNoForbiddenTokens(
            playerWindowSource + controller,
            ".Where(e => e.Entry.IsStreckenschaden",
            "StreckenschadenActionMapper.OpenEntry(");
        Assert.Contains("public static IReadOnlyList<StreckenschadenActionMapper.OpenEntry> BuildOpenEntries", builder);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_action_application_lives_in_applier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingStreckenschadenTrackingController.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplyCommandWorkflow.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplier.cs");

        Assert.True(File.Exists(workflowPath), "Streckenschaden-Aktions-Gate muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Streckenschaden-Aktionen muessen ausserhalb der PlayerWindow-Partials angewendet werden.");

        var playerWindowSource = ReadPlayerWindowSource(uiRoot);
        var controller = File.ReadAllText(controllerPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var applier = File.ReadAllText(applierPath);

        Assert.Contains("CodingStreckenschadenActionApplyCommandWorkflow.Execute", controller);
        Assert.Contains("CodingStreckenschadenActionApplier.Apply", controller);
        AssertNoForbiddenTokens(
            playerWindowSource + controller,
            "private void ApplyStreckenschadenActions",
            "StreckenschadenActionMapper.MapAll");
        AssertNoForbiddenTokens(
            playerWindowSource + controller,
            "if (codingSessionService == null || codingEvents == null || actions.Count == 0)",
            "codingSessionService.AddEvent(draft.Entry)",
            "codingSessionService.UpdateEvent");
        Assert.Contains("if (!request.HasCodingSessionService || !request.HasCodingEvents || !request.HasActions)", workflow);
        Assert.Contains("actions.ApplyActions()", workflow);
        Assert.Contains("StreckenschadenActionMapper.MapAll", applier);
        Assert.Contains("codingSessionService.AddEvent(draft.Entry)", applier);
        Assert.Contains("codingSessionService.UpdateEvent", applier);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_observation_projection_lives_in_builder()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingStreckenschadenTrackingController.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenObservationBuilder.cs");

        Assert.True(File.Exists(builderPath), "Segment-zu-Streckenschaden-Observation-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowSource = ReadPlayerWindowSource(uiRoot);
        var controller = File.ReadAllText(controllerPath);
        var builder = File.ReadAllText(builderPath);

        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", controller);
        AssertNoForbiddenTokens(
            playerWindowSource + controller,
            "new List<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation>",
            "observations.Add(new AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation");
        Assert.Contains("public static CodingStreckenschadenObservationBuildResult Build", builder);
        Assert.Contains("new StreckenschadenTracker.Observation", builder);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_tracking_lives_in_one_dedicated_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var oldPartialPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var exitFactoryPath = Path.Combine(windowsRoot, "PlayerWindowCodingModeExitControllerFactory.cs");
        var multiModelPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var boundaryPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Boundary.cs");
        var importPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingStreckenschadenTrackingController.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenTrackingCommandWorkflow.cs");

        Assert.False(File.Exists(oldPartialPath), "Die alte PlayerWindow-Streckenschaden-Teilklasse soll entfernt bleiben.");
        Assert.True(File.Exists(controllerPath), "Streckenschaden-Tracking soll in einem eigenen Controller liegen.");
        Assert.True(File.Exists(workflowPath), "Streckenschaden-Tracking-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var playerWindowSource = ReadPlayerWindowSource(uiRoot);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var exitFactory = File.ReadAllText(exitFactoryPath);
        var multiModel = File.ReadAllText(multiModelPath);
        var boundary = File.ReadAllText(boundaryPath);
        var import = File.ReadAllText(importPath);
        var controller = File.ReadAllText(controllerPath);
        var workflow = File.ReadAllText(workflowPath);

        AssertNoForbiddenTokens(
            playerWindowSource,
            "private HashSet<SegmentedFinding> ApplyStreckenschadenTracking",
            "private bool TryApplyStreckenschadenActions",
            "private void CloseTrackedStreckenschaeden",
            "CodingStreckenschadenTrackerOwner",
            "CodingStreckenschadenTrackingCommandWorkflow",
            "CodingStreckenschadenObservationBuilder",
            "CodingStreckenschadenActionApplier");
        Assert.Contains(
            "private readonly ICodingStreckenschadenTrackingController _codingStreckenschadenTrackingController;",
            state);
        Assert.Contains("public sealed class CodingStreckenschadenTrackingController", controller);
        Assert.Contains("CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking", controller);
        Assert.Contains("CodingStreckenschadenTrackingCommandWorkflow.CloseTracked", controller);
        AssertNoForbiddenTokens(
            controller,
            "if (codingSessionService == null || !_codingSessionHost.HasViewModel)",
            "var trackingInput = CodingStreckenschadenObservationBuilder.Build",
            "var actions = _streckenTracker.CloseAll",
            "if (TryApplyStreckenschadenActions(actions, videoTime))",
            "if (actions.Count == 0) return");
        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", controller);
        Assert.Contains("UpdateTracker: _trackerOwner.Update", controller);
        Assert.Contains("CloseAll: _trackerOwner.CloseAll", controller);
        Assert.Contains("CodingStreckenschadenActionApplier.Apply", controller);
        Assert.Contains("if (!request.HasCodingSessionService || !request.HasCodingViewModel)", workflow);
        Assert.Contains("actions.BuildObservations", workflow);
        Assert.Contains("actions.UpdateTracker", workflow);
        Assert.Contains("actions.ApplyActions", workflow);
        Assert.Contains("actions.RefreshEvents()", workflow);
        Assert.Contains("actions.CloseAll", workflow);

        Assert.Equal(1, CountOccurrences(playerWindowSource, "new CodingStreckenschadenTrackingController("));
        Assert.Contains("ResolveCodingSessionService: () => _codingSessionRuntimeOwner.Service", windowRoot);
        Assert.Contains("ResolveCode: _codingFindingContext.ResolveCode", windowRoot);
        Assert.Contains("LookupLabel: _codingFindingContext.LookupLabel", windowRoot);
        Assert.Contains("AttachAnalyzedFramePhoto: AttachAnalyzedFramePhoto", windowRoot);
        Assert.Contains("ResolveCurrentVideoTime: () => _playerTimelineHost.CurrentTimeOrZero", windowRoot);
        Assert.Contains("RefreshEvents: RefreshCodingEventsList", windowRoot);
        Assert.Contains("ApplyStretchTracking: _codingStreckenschadenTrackingController.ApplyTracking", multiModel);
        Assert.Contains("_codingStreckenschadenTrackingController.CloseTracked", boundary);
        Assert.Contains("ResetStretchTracker: _codingStreckenschadenTrackingController.Reset", import);
        Assert.Contains("dependencies.StreckenschadenTrackingController.CloseTracked", exitFactory);

        var refreshControllerIndex = windowRoot.IndexOf("_codingEventsRefreshController =", StringComparison.Ordinal);
        var stretchControllerIndex = windowRoot.IndexOf("_codingStreckenschadenTrackingController =", StringComparison.Ordinal);
        Assert.True(refreshControllerIndex >= 0 && refreshControllerIndex < stretchControllerIndex);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_close_marker_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenEventFactory.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageManualCloseApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventListActionWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionen sollen in einem eigenen Partial liegen.");
        Assert.True(File.Exists(applierPath), "Manuelles Streckenschaden-Schliessen soll ausserhalb der PlayerWindow-Partials angewendet werden.");
        Assert.True(File.Exists(workflowPath), "Streckenschaden-Schliessen soll ueber den Coding-Event-Listenworkflow laufen.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var factory = File.ReadAllText(factoryPath);
        var applier = File.ReadAllText(applierPath);
        var workflow = File.ReadAllText(workflowPath);

        AssertNoForbiddenTokens(events, "CodingStreckenschadenEventFactory.CloseStart");
        AssertNoForbiddenTokens(
            actions,
            "CodingStreckenschadenEventFactory.CloseStart",
            "CodingStretchDamageManualCloseApplier.Apply");
        Assert.Contains("CodingEventListActionWorkflow.CloseStretch", actions);
        Assert.Contains("CodingStretchDamageManualCloseApplier.Apply", workflow);
        Assert.Contains("CodingStreckenschadenEventFactory.CloseStart", applier);
        AssertNoForbiddenTokens(events + actions, "Beschreibung + \" (Ende)\"");
        Assert.Contains("public static ProtocolEntry CloseStart", factory);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_close_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageClosePolicy.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageManualCloseApplier.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionen sollen in einem eigenen Partial liegen.");
        Assert.True(File.Exists(policyPath), "Streckenschaden-Schliessregel muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Manuelles Streckenschaden-Schliessen soll die Policy ausserhalb der PlayerWindow-Partials nutzen.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var policy = File.ReadAllText(policyPath);
        var applier = File.ReadAllText(applierPath);

        AssertNoForbiddenTokens(
            events,
            "CodingStretchDamageClosePolicy.CanClose",
            "CodingStretchDamageClosePolicy.BuildClosedStatusText");
        AssertNoForbiddenTokens(
            actions,
            "CodingStretchDamageClosePolicy.CanClose",
            "CodingStretchDamageClosePolicy.BuildClosedStatusText");
        Assert.Contains("CodingStretchDamageClosePolicy.CanClose", applier);
        Assert.Contains("CodingStretchDamageClosePolicy.BuildClosedStatusText", applier);
        AssertNoForbiddenTokens(
            events + actions,
            "currentMeter <= (startEvent.MeterAtCapture + 0.01)",
            "Streckenschaden geschlossen:");
        Assert.Contains("public static bool CanClose", policy);
        Assert.Contains("CloseToleranceMeters = 0.01", policy);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = new List<string>();
        foreach (var token in forbiddenTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
                hits.Add(token);
        }

        Assert.True(
            hits.Count == 0,
            "Verbotene alte Streckenschaden-Logik gefunden: " + string.Join(", ", hits));
    }

    private static int CountOccurrences(string source, string token)
        => source.Split(token, StringSplitOptions.None).Length - 1;

    private static string ReadPlayerWindowSource(string uiRoot)
    {
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
    }
}
