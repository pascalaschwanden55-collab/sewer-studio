using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingEventsArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_select_code_handler_uses_fire_and_forget_wrapper()
    {
        var eventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Events.cs");

        var events = File.ReadAllText(eventsPath);

        Assert.Contains("private void CodingSelectCode_Click", events);
        Assert.Contains(".SafeFireAndForget(\"CodingSelectCode\")", events);
        Assert.Contains("private async Task HandleCodingSelectCodeAsync", events);
    }

    [Fact]
    public void PlayerWindow_coding_event_display_order_lives_in_policy()
    {
        var eventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventDisplayOrderPolicy.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventsListControls.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventsRefreshWorkflow.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventsListRefreshCommandWorkflow.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingEventsRefreshController.cs");

        Assert.True(File.Exists(policyPath), "Codier-Ereignis-Sortierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Codier-Ereignislisten-Rebind muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(workflowPath), "Codier-Ereignislisten-Refresh soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Codier-Ereignislisten-Refresh-Befehl soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(controllerPath), "Codier-Ereignislisten-Refresh soll einen eigenen Controller besitzen.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.Contains("_codingEventsRefreshController.RefreshList", events);
        Assert.DoesNotContain("CodingEventsRefreshWorkflow.RefreshListAndStatistics", events, StringComparison.Ordinal);
        Assert.Contains("CodingEventsRefreshWorkflow.RefreshListAndStatistics", controller);
        Assert.Contains("CodingEventsListRefreshCommandWorkflow.Execute", controller);
        Assert.Contains("public static IReadOnlyList<CodingEvent> Order", policy);
        Assert.Contains("public sealed class CodingEventsListControls", controls);
        Assert.Contains("_eventsList.ItemsSource", controls);
        Assert.Contains("CodingEventDisplayOrderPolicy.Order", workflow);
        Assert.Contains("listControls.ApplyOrderedEvents", workflow);
        Assert.Contains("actions.ScheduleColorize()", commandWorkflow);
        Assert.Contains("public sealed class CodingEventsRefreshController", controller);
    }

    [Fact]
    public void PlayerWindow_coding_event_list_surface_uses_controls()
    {
        var eventsControlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventsListControls.cs");
        var importControlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingImportReferenceControls.cs");
        var relevantPartialPaths = new[]
        {
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindowCodingModeExitControllerFactory.cs"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.ImportReference.cs"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Timeline.cs"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs")
        };

        Assert.True(File.Exists(eventsControlsPath), "Coding-Event-Listenoberflaeche soll ueber CodingEventsListControls laufen.");
        Assert.True(File.Exists(importControlsPath), "Import-Referenzliste soll ueber CodingImportReferenceControls laufen.");

        var joinedPartials = string.Join(
            Environment.NewLine,
            relevantPartialPaths.Select(File.ReadAllText));
        var eventsControls = File.ReadAllText(eventsControlsPath);
        var importControls = File.ReadAllText(importControlsPath);

        Assert.Contains("_codingSidePanelControllers.EventsList.SelectEvent", joinedPartials);
        Assert.Contains("_codingSidePanelControllers.EventsList.SetItemsSource", joinedPartials);
        Assert.Contains("CodingImportReferenceControls.SetItemsSource", joinedPartials);
        Assert.Contains("CodingImportReferenceControls.ClearItemsSource", joinedPartials);
        Assert.Contains("public void SelectEvent", eventsControls);
        Assert.Contains("public void SetItemsSource", eventsControls);
        Assert.Contains("public static void SetItemsSource", importControls);
        Assert.Contains("public static void ClearItemsSource", importControls);
    }

    [Fact]
    public void PlayerWindow_manual_code_meter_resolution_uses_policy()
    {
        var eventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var markingTrainingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Training.cs");
        var manualMarkWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Live", "LiveDetectionManualMarkTrainingWorkflow.cs");
        var resolverPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingCurrentMeterResolver.cs");

        var events = File.ReadAllText(eventsPath);
        var markingTraining = File.ReadAllText(markingTrainingPath);
        var manualMarkWorkflow = File.Exists(manualMarkWorkflowPath) ? File.ReadAllText(manualMarkWorkflowPath) : "";
        var resolver = File.ReadAllText(resolverPath);

        Assert.Contains("CodingCurrentMeterResolver.ResolveManualEntry", events);
        Assert.Contains("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", manualMarkWorkflow);
        Assert.Contains("public static double ResolveManualEntry", resolver);
        Assert.Contains("public static double ParseDisplayedMeterOrZero", resolver);
    }

    [Fact]
    public void PlayerWindow_manual_coding_ai_context_lives_in_factory()
    {
        var eventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var factoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingManualEventFactory.cs");
        var appenderPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingManualEventAppender.cs");
        var selectedCodeWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingSelectedCodeEventWorkflow.cs");
        var selectCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingSelectCodeCommandWorkflow.cs");
        var manualEntryWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingCodeExplorerManualEntryWorkflow.cs");
        var createCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingCreateSelectedCodeEventCommandWorkflow.cs");
        var postWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventCreationPostWorkflow.cs");
        var accessorsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");
        var sidePanelControllerSetPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSidePanelControllerSet.cs");

        var events = File.ReadAllText(eventsPath);
        var factory = File.ReadAllText(factoryPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var selectedCodeWorkflow = File.Exists(selectedCodeWorkflowPath) ? File.ReadAllText(selectedCodeWorkflowPath) : "";
        var selectCommandWorkflow = File.Exists(selectCommandWorkflowPath) ? File.ReadAllText(selectCommandWorkflowPath) : "";
        var manualEntryWorkflow = File.Exists(manualEntryWorkflowPath) ? File.ReadAllText(manualEntryWorkflowPath) : "";
        var createCommandWorkflow = File.Exists(createCommandWorkflowPath) ? File.ReadAllText(createCommandWorkflowPath) : "";
        var postWorkflow = File.Exists(postWorkflowPath) ? File.ReadAllText(postWorkflowPath) : "";
        var accessors = File.ReadAllText(accessorsPath);
        var sidePanelControllerSet = File.Exists(sidePanelControllerSetPath) ? File.ReadAllText(sidePanelControllerSetPath) : "";
        Assert.True(File.Exists(selectedCodeWorkflowPath), "Manueller Selected-Code-Event-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(selectCommandWorkflowPath), "Manueller Select-Code-Button-Ablauf soll ausserhalb der Events-Partial orchestriert werden.");
        Assert.True(File.Exists(manualEntryWorkflowPath), "Manuelle Code-Explorer-Eintragserzeugung soll ausserhalb der Events-Partial orchestriert werden.");
        Assert.True(File.Exists(createCommandWorkflowPath), "Manueller Create-Event-Button-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(postWorkflowPath), "Nachbearbeitung manuell erzeugter Coding-Events soll ausserhalb der Events-Partial orchestriert werden.");
        Assert.Contains("CodingSelectCodeCommandWorkflow.ExecuteAsync", events);
        Assert.Contains("CodingCodeExplorerManualEntryWorkflow.Execute", events);
        Assert.Contains("CodingCreateSelectedCodeEventCommandWorkflow.Execute", events);
        Assert.Contains("CodingSelectedCodeEventWorkflow.Create", events);
        Assert.Contains("CodingManualEventAppender.Apply", events);
        Assert.Contains("CodingEventCreationPostWorkflow.Apply", events);
        Assert.Contains("_codingSessionHost", events);
        Assert.Contains(".CreateManualEntry(", manualEntryWorkflow);
        Assert.Contains("CodingManualEventFactory.CreateUnconfirmed", selectedCodeWorkflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddIfPresent", selectedCodeWorkflow);
        Assert.Contains("CodingManualEventAppender.Apply", selectedCodeWorkflow);
        Assert.Contains("actions.PauseForCodingInteraction()", selectCommandWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlayInputAsync", selectCommandWorkflow);
        Assert.Contains("actions.ReadOsdMeterAsync()", selectCommandWorkflow);
        Assert.Contains("actions.ResolveManualEntryMeter(osdMeter)", selectCommandWorkflow);
        Assert.Contains("actions.CreateManualEntry(", selectCommandWorkflow);
        Assert.Contains("actions.ApplyPostCreation(createdEvent)", selectCommandWorkflow);
        Assert.Contains("actions.GetCurrentVideoTime()", createCommandWorkflow);
        Assert.Contains("actions.SetCurrentVideoTime(videoTime)", createCommandWorkflow);
        Assert.Contains("actions.CreateEvent(videoTime)", createCommandWorkflow);
        Assert.Contains("actions.ApplyPostCreation(createdEvent)", createCommandWorkflow);
        Assert.Contains("public static bool Apply", postWorkflow);
        Assert.Contains("new CodingEventCreationPostActions", sidePanelControllerSet);
        Assert.Contains("_codingSessionHost", accessors);
        Assert.Contains("CodingManualEventFactory.CreateUnconfirmedContext", appender);
        Assert.Contains("public static CodingEventReviewContext CreateUnconfirmedContext", factory);
    }
}
