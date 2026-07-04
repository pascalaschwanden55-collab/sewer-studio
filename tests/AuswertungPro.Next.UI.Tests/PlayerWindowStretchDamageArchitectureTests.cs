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
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
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

        var boundaries = File.ReadAllText(boundariesPath);
        var closePrompt = File.ReadAllText(closePromptPath);
        var policy = File.ReadAllText(policyPath);
        var closePolicy = File.ReadAllText(closePolicyPath);
        var closeApplier = File.ReadAllText(closeApplierPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var dialogWorkflow = File.Exists(dialogWorkflowPath) ? File.ReadAllText(dialogWorkflowPath) : "";
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);

        AssertNoForbiddenTokens(boundaries, "private bool CloseOpenStreckenschaeden");
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
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionInputBuilder.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplier.cs");

        Assert.True(File.Exists(builderPath), "Mapper-Eingabe fuer Streckenschaden-Aktionen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Streckenschaden-Aktionsausfuehrung muss den Action-Input-Builder nutzen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var builder = File.ReadAllText(builderPath);
        var applier = File.ReadAllText(applierPath);

        Assert.Contains("CodingStreckenschadenActionInputBuilder.BuildOpenEntries", applier);
        AssertNoForbiddenTokens(
            ai + strecken,
            ".Where(e => e.Entry.IsStreckenschaden",
            "StreckenschadenActionMapper.OpenEntry(");
        Assert.Contains("public static IReadOnlyList<StreckenschadenActionMapper.OpenEntry> BuildOpenEntries", builder);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_action_application_lives_in_applier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplyCommandWorkflow.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplier.cs");

        Assert.True(File.Exists(workflowPath), "Streckenschaden-Aktions-Gate muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Streckenschaden-Aktionen muessen ausserhalb der PlayerWindow-Partials angewendet werden.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var applier = File.ReadAllText(applierPath);

        Assert.Contains("CodingStreckenschadenActionApplyCommandWorkflow.Execute", strecken);
        Assert.Contains("CodingStreckenschadenActionApplier.Apply", strecken);
        AssertNoForbiddenTokens(
            ai + strecken,
            "private void ApplyStreckenschadenActions",
            "StreckenschadenActionMapper.MapAll");
        AssertNoForbiddenTokens(
            strecken,
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
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenObservationBuilder.cs");

        Assert.True(File.Exists(builderPath), "Segment-zu-Streckenschaden-Observation-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var builder = File.ReadAllText(builderPath);

        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", strecken);
        AssertNoForbiddenTokens(
            ai + strecken,
            "new List<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation>",
            "observations.Add(new AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation");
        Assert.Contains("public static CodingStreckenschadenObservationBuildResult Build", builder);
        Assert.Contains("new StreckenschadenTracker.Observation", builder);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_tracking_lives_in_ai_stretch_damage_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenTrackingCommandWorkflow.cs");
        var trackerOwnerPath = Path.Combine(uiRoot, "Player", "CodingStreckenschadenTrackerOwner.cs");

        Assert.True(File.Exists(streckenPath), "Streckenschaden-Tracking soll aus dem allgemeinen AI-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Streckenschaden-Tracking-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(trackerOwnerPath), "Streckenschaden-Tracker-Besitz soll nicht als Rohfeld im PlayerWindow liegen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var state = File.ReadAllText(statePath);
        var workflow = File.ReadAllText(workflowPath);
        var trackerOwner = File.Exists(trackerOwnerPath) ? File.ReadAllText(trackerOwnerPath) : "";

        AssertNoForbiddenTokens(
            ai,
            "private HashSet<SegmentedFinding> ApplyStreckenschadenTracking",
            "private void ApplyStreckenschadenActions",
            "private void CloseTrackedStreckenschaeden");
        AssertNoForbiddenTokens(state, "private readonly StreckenschadenTracker _streckenTracker = new();");
        Assert.Contains("private readonly CodingStreckenschadenTrackerOwner _streckenschadenTracker = new();", state);
        Assert.Contains("private HashSet<SegmentedFinding> ApplyStreckenschadenTracking", strecken);
        Assert.Contains("CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking", strecken);
        Assert.Contains("CodingStreckenschadenTrackingCommandWorkflow.CloseTracked", strecken);
        AssertNoForbiddenTokens(
            strecken,
            "if (codingSessionService == null || !_codingSessionHost.HasViewModel)",
            "var trackingInput = CodingStreckenschadenObservationBuilder.Build",
            "var actions = _streckenTracker.CloseAll",
            "if (TryApplyStreckenschadenActions(actions, videoTime))",
            "if (actions.Count == 0) return");
        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", strecken);
        Assert.Contains("UpdateTracker: _streckenschadenTracker.Update", strecken);
        Assert.Contains("CloseAll: _streckenschadenTracker.CloseAll", strecken);
        Assert.Contains("CodingStreckenschadenActionApplier.Apply", strecken);
        Assert.Contains("if (!request.HasCodingSessionService || !request.HasCodingViewModel)", workflow);
        Assert.Contains("actions.BuildObservations", workflow);
        Assert.Contains("actions.UpdateTracker", workflow);
        Assert.Contains("actions.ApplyActions", workflow);
        Assert.Contains("actions.RefreshEvents()", workflow);
        Assert.Contains("actions.CloseAll", workflow);
        Assert.Contains("public sealed class CodingStreckenschadenTrackerOwner", trackerOwner);
        Assert.Contains("public IReadOnlyList<StreckenschadenTracker.SegmentAction> Update", trackerOwner);
        Assert.Contains("public IReadOnlyList<StreckenschadenTracker.SegmentAction> CloseAll", trackerOwner);
        Assert.Contains("public void Reset", trackerOwner);
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
}
