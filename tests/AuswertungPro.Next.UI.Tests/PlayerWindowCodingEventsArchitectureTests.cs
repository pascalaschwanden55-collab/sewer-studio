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
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");

        var events = File.ReadAllText(eventsPath);

        Assert.DoesNotContain("private async void CodingSelectCode_Click", events);
        Assert.Contains("private void CodingSelectCode_Click", events);
        Assert.Contains(".SafeFireAndForget(\"CodingSelectCode\")", events);
        Assert.Contains("private async Task HandleCodingSelectCodeAsync", events);
    }

    [Fact]
    public void PlayerWindow_coding_event_display_order_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEventDisplayOrderPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingEventsListControls.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsRefreshWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsListRefreshCommandWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Codier-Ereignis-Sortierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Codier-Ereignislisten-Rebind muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(workflowPath), "Codier-Ereignislisten-Refresh soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Codier-Ereignislisten-Refresh-Befehl soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingEventsRefreshWorkflow.RefreshListAndStatistics", events);
        Assert.Contains("CodingEventsListRefreshCommandWorkflow.Execute", events);
        Assert.DoesNotContain("CodingEventDisplayOrderPolicy.Order", events);
        Assert.DoesNotContain("_codingEventsListControls.ApplyOrderedEvents", events);
        Assert.DoesNotContain("if (!CodingEventsRefreshWorkflow.RefreshListAndStatistics", events);
        Assert.DoesNotContain(".OrderBy(e => e.MeterAtCapture)", events);
        Assert.DoesNotContain("LstCodingEvents.ItemsSource", events);
        Assert.DoesNotContain("_codingVm.Events.Clear()", events);
        Assert.Contains("public static IReadOnlyList<CodingEvent> Order", policy);
        Assert.Contains("public sealed class CodingEventsListControls", controls);
        Assert.Contains("_eventsList.ItemsSource", controls);
        Assert.Contains("CodingEventDisplayOrderPolicy.Order", workflow);
        Assert.Contains("listControls.ApplyOrderedEvents", workflow);
        Assert.Contains("actions.ScheduleColorize()", commandWorkflow);
    }

    [Fact]
    public void PlayerWindow_coding_event_list_surface_uses_controls()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsControlsPath = Path.Combine(uiRoot, "Ai", "CodingEventsListControls.cs");
        var importControlsPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceControls.cs");
        var relevantPartials = new[]
        {
            "PlayerWindow.Coding.Confirmation.cs",
            "PlayerWindow.Coding.Lifecycle.Exit.cs",
            "PlayerWindow.Coding.Lifecycle.ImportReference.cs",
            "PlayerWindow.Coding.Lifecycle.Timeline.cs",
            "PlayerWindow.CodingSidePanelAccessors.cs"
        };

        Assert.True(File.Exists(eventsControlsPath), "Coding-Event-Listenoberflaeche soll ueber CodingEventsListControls laufen.");
        Assert.True(File.Exists(importControlsPath), "Import-Referenzliste soll ueber CodingImportReferenceControls laufen.");

        var joinedPartials = string.Join(
            Environment.NewLine,
            relevantPartials.Select(file => File.ReadAllText(Path.Combine(windowsRoot, file))));
        var eventsControls = File.ReadAllText(eventsControlsPath);
        var importControls = File.ReadAllText(importControlsPath);

        Assert.Contains("_codingSidePanelControllers.EventsList.SelectEvent", joinedPartials);
        Assert.Contains("_codingSidePanelControllers.EventsList.SetItemsSource", joinedPartials);
        Assert.Contains("CodingImportReferenceControls.SetItemsSource", joinedPartials);
        Assert.Contains("CodingImportReferenceControls.ClearItemsSource", joinedPartials);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem =", joinedPartials);
        Assert.DoesNotContain("LstCodingEvents.ItemsSource =", joinedPartials);
        Assert.DoesNotContain("LstImportEvents.ItemsSource =", joinedPartials);
        Assert.Contains("public void SelectEvent", eventsControls);
        Assert.Contains("public void SetItemsSource", eventsControls);
        Assert.Contains("public static void SetItemsSource", importControls);
        Assert.Contains("public static void ClearItemsSource", importControls);
    }

    [Fact]
    public void PlayerWindow_manual_code_meter_resolution_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var markingTrainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Training.cs");
        var manualMarkWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingWorkflow.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingCurrentMeterResolver.cs");

        var events = File.ReadAllText(eventsPath);
        var markingTraining = File.ReadAllText(markingTrainingPath);
        var manualMarkWorkflow = File.Exists(manualMarkWorkflowPath) ? File.ReadAllText(manualMarkWorkflowPath) : "";
        var resolver = File.ReadAllText(resolverPath);

        Assert.Contains("CodingCurrentMeterResolver.ResolveManualEntry", events);
        Assert.DoesNotContain("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", markingTraining);
        Assert.Contains("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", manualMarkWorkflow);
        Assert.DoesNotContain("Math.Round(Math.Max(0, osdMeter", events);
        Assert.DoesNotContain("TxtCodingMeter?.Text?.Replace(\"m\"", markingTraining);
        Assert.Contains("public static double ResolveManualEntry", resolver);
        Assert.Contains("public static double ParseDisplayedMeterOrZero", resolver);
    }

    [Fact]
    public void PlayerWindow_manual_coding_ai_context_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingManualEventFactory.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingManualEventAppender.cs");
        var selectedCodeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSelectedCodeEventWorkflow.cs");
        var selectCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSelectCodeCommandWorkflow.cs");
        var manualEntryWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerManualEntryWorkflow.cs");
        var createCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCreateSelectedCodeEventCommandWorkflow.cs");
        var postWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventCreationPostWorkflow.cs");
        var accessorsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");
        var sidePanelControllerSetPath = Path.Combine(uiRoot, "Player", "CodingSidePanelControllerSet.cs");

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
        Assert.DoesNotContain("_codingVm", events);
        Assert.DoesNotContain("_codingSchemaManager.Cancel()", events);
        Assert.DoesNotContain("_codingVm.CurrentOverlay = null", events);
        Assert.DoesNotContain("TxtCodingSelectedCode.Text = \"\"", events);
        Assert.DoesNotContain("BtnCodingCreateEvent.IsEnabled = false", events);
        Assert.DoesNotContain(".CreateManualEntry(", events);
        Assert.DoesNotContain("CodingManualEventFactory.CreateUnconfirmed", events);
        Assert.DoesNotContain("CodingManualEventFactory.CreateUnconfirmedContext", events);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender", events);
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
        Assert.DoesNotContain("new CodingEventsListControls", accessors);
        Assert.DoesNotContain("new CodingStatisticsControls", accessors);
        Assert.DoesNotContain("new CodingInlineDefectDetailControls", accessors);
        Assert.DoesNotContain("new CodingEventCreationPostActions", accessors);
        Assert.Contains("new CodingEventCreationPostActions", sidePanelControllerSet);
        Assert.Contains("_codingSessionHost", accessors);
        Assert.DoesNotContain("_codingVm", accessors);
        Assert.Contains("CodingManualEventFactory.CreateUnconfirmedContext", appender);
        Assert.DoesNotContain("new CodingEventAiContext", events);
        Assert.Contains("public static CodingEventAiContext CreateUnconfirmedContext", factory);
    }
}
