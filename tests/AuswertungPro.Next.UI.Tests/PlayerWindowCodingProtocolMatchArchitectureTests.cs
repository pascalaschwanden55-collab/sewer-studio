using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingProtocolMatchArchitectureTests
{
    [Fact]
    public void PlayerWindow_import_confirmation_badge_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowService.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : string.Empty;

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge", workflow);
        Assert.DoesNotContain("bestaetigt", training);
        Assert.DoesNotContain("Interval = TimeSpan.FromSeconds(3)", training);
        Assert.Contains("public static CodingImportConfirmationBadgeState BuildImportConfirmationBadge", policy);
    }

    [Fact]
    public void PlayerWindow_green_match_accept_overlay_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var runnerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolGreenMatchTrainingRunner.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";

        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", training);
        Assert.DoesNotContain("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", training);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", runner);
        Assert.DoesNotContain("gruene Treffer als Training uebernommen", training);
        Assert.DoesNotContain("ShowOverlay($\"{accepted}", training);
        Assert.Contains("public static CodingProtocolMatchOverlayState BuildAcceptedGreenMatchesOverlay", policy);
    }

    [Fact]
    public void PlayerWindow_protocol_match_summary_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolMatchPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var importSeekWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingImportEventSeekCommandWorkflow.cs");
        var matchCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchCommandWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchSummaryControls.cs");

        Assert.True(File.Exists(importSeekWorkflowPath), "Import-Event-Seek-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchCommandWorkflowPath), "Protocol-Match-Ausfuehrungsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Protocol-Match-Summary-Control-Zuweisung soll ausserhalb des PlayerWindow-Partials liegen.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var importSeekWorkflow = File.Exists(importSeekWorkflowPath) ? File.ReadAllText(importSeekWorkflowPath) : "";
        var matchCommandWorkflow = File.Exists(matchCommandWorkflowPath) ? File.ReadAllText(matchCommandWorkflowPath) : "";
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        Assert.Contains("CodingImportEventSeekCommandWorkflow.Execute", protocolMatch);
        Assert.Contains("CodingProtocolMatchCommandWorkflow.Execute", protocolMatch);
        Assert.Contains("CodingProtocolMatchSummaryControls.Apply", protocolMatch);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleLoaded", protocolMatch);
        Assert.DoesNotContain("Dispatcher.InvokeAsync", protocolMatch);
        Assert.Contains("_codingSessionHost", protocolMatch);
        Assert.DoesNotContain("_codingVm", protocolMatch);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", protocolMatch);
        Assert.DoesNotContain("_lastCodingMatch = CodingProtocolMatchRunner.Run", protocolMatch);
        Assert.DoesNotContain("CodingEventSeekPolicy.TryGetSeekMilliseconds", protocolMatch);
        Assert.DoesNotContain("importEvent.MeterAtCapture > 0", protocolMatch);
        Assert.DoesNotContain("_codingSessionRuntimeOwner.Service.MoveToMeter(importEvent.MeterAtCapture)", protocolMatch);
        Assert.Contains("CodingEventSeekPolicy.TryGetSeekMilliseconds(importEvent", importSeekWorkflow);
        Assert.Contains("importEvent.MeterAtCapture <= 0", importSeekWorkflow);
        Assert.Contains("actions.MoveToMeter(importEvent.MeterAtCapture)", importSeekWorkflow);
        Assert.Contains("actions.MarkNavigationPending()", importSeekWorkflow);
        Assert.Contains("actions.SyncVideoToCodingMeter()", importSeekWorkflow);
        Assert.Contains("if (!request.HasCodingViewModel)", matchCommandWorkflow);
        Assert.Contains("var routing = actions.RunMatch()", matchCommandWorkflow);
        Assert.Contains("actions.StoreMatch(routing)", matchCommandWorkflow);
        Assert.Contains("actions.UpdateSummary(routing)", matchCommandWorkflow);
        Assert.Contains("actions.RefreshEvents()", matchCommandWorkflow);
        Assert.Contains("actions.ScheduleHighlights()", matchCommandWorkflow);
        Assert.DoesNotContain("TxtCodingProtocolMatchSummary.Text", protocolMatch);
        Assert.DoesNotContain("BtnAcceptGreenCodingMatches.IsEnabled", protocolMatch);
        Assert.Contains("CodingProtocolMatchSummaryFormatter.Format", controls);
        Assert.Contains("CodingProtocolMatchSummaryFormatter.CanAcceptGreenMatches", controls);
    }

    [Fact]
    public void PlayerWindow_protocol_match_training_lives_in_training_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var protocolMatchPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var acceptGreenCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAcceptGreenMatchesCommandWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingImportConfirmCommandWorkflow.cs");
        var confirmWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingConfirmationWorkflow.cs");
        var importTrainingResultWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingImportTrainingResultWorkflow.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.True(File.Exists(trainingPath), "ProtocolMatch-Trainingsuebernahme soll aus dem Match-Partial heraus.");
        Assert.True(File.Exists(acceptGreenCommandWorkflowPath), "Green-Match-Accept-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Import-Confirm-Auswahlentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(confirmWorkflowPath), "Import-Confirm-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(importTrainingResultWorkflowPath), "Import-Training-Ergebnisbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "ProtocolMatch-Trainingsworkflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "ProtocolMatch-Trainingsworkflow soll ueber Factory verdrahtet werden.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var training = File.ReadAllText(trainingPath);
        var acceptGreenCommandWorkflow = File.Exists(acceptGreenCommandWorkflowPath) ? File.ReadAllText(acceptGreenCommandWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var confirmWorkflow = File.Exists(confirmWorkflowPath) ? File.ReadAllText(confirmWorkflowPath) : "";
        var importTrainingResultWorkflow = File.Exists(importTrainingResultWorkflowPath) ? File.ReadAllText(importTrainingResultWorkflowPath) : "";
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);
        Assert.DoesNotContain("private async void CodingAcceptGreenMatches_Click", protocolMatch);
        Assert.DoesNotContain("private async void ImportConfirm_Click", protocolMatch);
        Assert.DoesNotContain("private async Task<bool> ConfirmImportAsTrainingAsync", protocolMatch);
        Assert.DoesNotContain("private async void CodingAcceptGreenMatches_Click", training);
        Assert.DoesNotContain("private async void ImportConfirm_Click", training);
        Assert.Contains("private void CodingAcceptGreenMatches_Click", training);
        Assert.Contains("private void ImportConfirm_Click", training);
        Assert.Contains(".SafeFireAndForget(\"CodingAcceptGreenMatches\")", training);
        Assert.Contains(".SafeFireAndForget(\"ImportConfirm\")", training);
        Assert.Contains("private async Task HandleCodingAcceptGreenMatchesAsync", training);
        Assert.Contains("CodingAcceptGreenMatchesCommandWorkflow.ExecuteAsync", training);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", training);
        Assert.DoesNotContain("if (_lastCodingMatch == null)", training);
        Assert.Contains("if (!request.HasCodingViewModel)", acceptGreenCommandWorkflow);
        Assert.Contains("actions.RunProtocolMatch()", acceptGreenCommandWorkflow);
        Assert.Contains("routing = actions.GetCurrentRouting()", acceptGreenCommandWorkflow);
        Assert.Contains("actions.AcceptGreenMatchesAsync(routing)", acceptGreenCommandWorkflow);
        Assert.Contains("actions.ShowOverlay(overlay.Value)", acceptGreenCommandWorkflow);
        Assert.Contains("private async Task HandleImportConfirmAsync", training);
        Assert.Contains("CodingImportConfirmCommandWorkflow.ExecuteAsync", training);
        Assert.DoesNotContain("LstImportEvents.SelectedItem is not CodingEvent", training);
        Assert.Contains("request.SelectedItem is not CodingEvent", commandWorkflow);
        Assert.Contains("actions.ConfirmImportAsTrainingAsync(importEvent)", commandWorkflow);
        Assert.Contains("private async Task<bool> ConfirmImportAsTrainingAsync", training);
        Assert.DoesNotContain("CodingProtocolImportTrainingWorkflowServiceFactory.Create", training);
        Assert.DoesNotContain("new CodingProtocolImportTrainingConfirmationWorkflowActions", training);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", confirmWorkflow);
        Assert.Contains("new CodingProtocolImportTrainingConfirmationWorkflowActions", confirmWorkflow);
        Assert.Contains("CodingProtocolImportTrainingConfirmationWorkflow.ConfirmAsync", training);
        Assert.Contains("CodingProtocolGuidedVerificationAdapter.Create", training);
        Assert.Contains("_codingAiRuntimeOwner.Controller.ProtocolVerifier", training);
        Assert.DoesNotContain(".ConfirmAsync(importEvent)", training);
        Assert.Contains("service.ConfirmAsync(importEvent)", confirmWorkflow);
        Assert.Contains("CodingImportTrainingResultWorkflow.Execute", training);
        Assert.DoesNotContain("new CodingImportTrainingResultActions", training);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", training);
        Assert.DoesNotContain("if (!result.Accepted)", training);
        Assert.DoesNotContain("var badge = result.Badge", training);
        Assert.Contains("if (!importResult.Accepted)", importTrainingResultWorkflow);
        Assert.Contains("new CodingImportTrainingResultActions", importTrainingResultWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", importTrainingResultWorkflow);
        Assert.Contains("actions.ShowBadge(badge.Text)", importTrainingResultWorkflow);
        Assert.Contains("actions.ScheduleHideBadge(badge.AutoHideDelay)", importTrainingResultWorkflow);
        Assert.Contains("_codingSessionHost", training);
        Assert.DoesNotContain("_codingVm", training);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync", training);
        Assert.DoesNotContain("LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation", training);
        Assert.DoesNotContain("CodingProtocolTrainingSnapshotStoreFactory.Create", training);
        Assert.Contains("CodingProtocolTrainingSnapshotStore", workflow);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation", workflowFactory);
        Assert.Contains("TeacherAnnotationStore.AppendAsync", workflowFactory);
    }

    [Fact]
    public void PlayerWindow_protocol_match_highlighting_lives_in_highlighting_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var protocolMatchPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.cs");
        var highlightingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Highlighting.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchHighlightControls.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchListHighlightWorkflow.cs");

        Assert.True(File.Exists(highlightingPath), "ProtocolMatch-Listenhighlighting soll aus dem Match-Partial heraus.");
        Assert.True(File.Exists(controlsPath), "ProtocolMatch-Listenhighlighting-Control-Zuweisung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "ProtocolMatch-Listenhighlighting-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var highlighting = File.ReadAllText(highlightingPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("private void ApplyCodingProtocolMatchListHighlights()", protocolMatch);
        Assert.DoesNotContain("private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)", protocolMatch);
        Assert.Contains("private void ApplyCodingProtocolMatchListHighlights()", highlighting);
        Assert.Contains("private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)", highlighting);
        Assert.Contains("CodingProtocolMatchListHighlightWorkflow.Execute", highlighting);
        Assert.DoesNotContain("for (var i = 0; i < listBox.Items.Count; i++)", highlighting);
        Assert.Contains("CodingProtocolMatchHighlightControls.Clear", highlighting);
        Assert.Contains("CodingProtocolMatchHighlightControls.Apply", highlighting);
        Assert.Contains("actions.HighlightItem(i)", workflow);
        Assert.DoesNotContain("CodingProtocolMatchDisplayPolicy.BackgroundColor", highlighting);
        Assert.DoesNotContain("CodingProtocolMatchDisplayPolicy.BadgeText", highlighting);
        Assert.DoesNotContain("badge.Visibility = Visibility.Visible", highlighting);
        Assert.DoesNotContain("emptyBadge.Visibility = Visibility.Collapsed", highlighting);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BackgroundColor", controls);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BadgeText", controls);
    }
}
