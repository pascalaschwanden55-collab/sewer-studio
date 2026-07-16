using System.Collections.Generic;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingProtocolMatchArchitectureTests
{
    [Fact]
    public void PlayerWindow_import_confirmation_badge_uses_display_policy()
    {
        var trainingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolImportTrainingWorkflowService.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : string.Empty;

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge", workflow);
        AssertNoForbiddenTokens(
            training,
            "bestaetigt",
            "Interval = TimeSpan.FromSeconds(3)");
        Assert.Contains("public static CodingImportConfirmationBadgeState BuildImportConfirmationBadge", policy);
    }

    [Fact]
    public void PlayerWindow_green_match_accept_overlay_uses_display_policy()
    {
        var trainingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var runnerPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolGreenMatchTrainingRunner.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";

        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", training);
        AssertNoForbiddenTokens(
            training,
            "CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay",
            "gruene Treffer als Training uebernommen",
            "ShowOverlay($\"{accepted}");
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", runner);
        Assert.Contains("public static CodingProtocolMatchOverlayState BuildAcceptedGreenMatchesOverlay", policy);
    }

    [Fact]
    public void PlayerWindow_protocol_match_summary_uses_controls_adapter()
    {
        var protocolMatchPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingProtocolMatchController.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var importSeekWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportEventSeekCommandWorkflow.cs");
        var matchCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolMatchCommandWorkflow.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolMatchSummaryControls.cs");

        Assert.True(File.Exists(importSeekWorkflowPath), "Import-Event-Seek-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchCommandWorkflowPath), "Protocol-Match-Ausfuehrungsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Protocol-Match-Summary-Control-Zuweisung soll ausserhalb des PlayerWindow-Partials liegen.");
        Assert.False(File.Exists(protocolMatchPath), "Der Protokollabgleich darf nicht wieder als PlayerWindow-Partial erscheinen.");
        Assert.True(File.Exists(controllerPath), "Der Protokollabgleich braucht einen eigenen Controller.");

        var controller = File.ReadAllText(controllerPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var importSeekWorkflow = File.Exists(importSeekWorkflowPath) ? File.ReadAllText(importSeekWorkflowPath) : "";
        var matchCommandWorkflow = File.Exists(matchCommandWorkflowPath) ? File.ReadAllText(matchCommandWorkflowPath) : "";
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        Assert.Contains("public interface ICodingProtocolMatchController", controller);
        Assert.Contains("public sealed class CodingProtocolMatchController", controller);
        Assert.Contains("CodingImportEventSeekCommandWorkflow.Execute", controller);
        Assert.Contains("CodingProtocolMatchCommandWorkflow.Execute", controller);
        Assert.Contains("private readonly ICodingProtocolMatchController _codingProtocolMatchController", state);
        Assert.Contains("CodingProtocolMatchSummaryControls.Apply", windowRoot);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleLoaded", windowRoot);
        Assert.Contains("_codingSessionHost", windowRoot);
        AssertNoForbiddenTokens(
            controller,
            "Dispatcher.InvokeAsync",
            "if (!_codingSessionHost.HasViewModel) return",
            "_lastCodingMatch = CodingProtocolMatchRunner.Run",
            "CodingEventSeekPolicy.TryGetSeekMilliseconds",
            "importEvent.MeterAtCapture > 0",
            "_codingSessionRuntimeOwner.Service.MoveToMeter(importEvent.MeterAtCapture)",
            "TxtCodingProtocolMatchSummary.Text",
            "BtnAcceptGreenCodingMatches.IsEnabled");
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
        Assert.Contains("CodingProtocolMatchSummaryFormatter.Format", controls);
        Assert.Contains("CodingProtocolMatchSummaryFormatter.CanAcceptGreenMatches", controls);
    }

    [Fact]
    public void PlayerWindow_protocol_match_training_lives_in_training_partial()
    {
        var protocolMatchPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingProtocolMatchController.cs");
        var trainingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var acceptGreenCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAcceptGreenMatchesCommandWorkflow.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportConfirmCommandWorkflow.cs");
        var confirmWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolImportTrainingConfirmationWorkflow.cs");
        var importTrainingResultWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportTrainingResultWorkflow.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolImportTrainingWorkflowService.cs");
        var workflowFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.True(File.Exists(trainingPath), "ProtocolMatch-Trainingsuebernahme soll aus dem Match-Partial heraus.");
        Assert.True(File.Exists(acceptGreenCommandWorkflowPath), "Green-Match-Accept-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Import-Confirm-Auswahlentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(confirmWorkflowPath), "Import-Confirm-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(importTrainingResultWorkflowPath), "Import-Training-Ergebnisbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "ProtocolMatch-Trainingsworkflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "ProtocolMatch-Trainingsworkflow soll ueber Factory verdrahtet werden.");

        Assert.False(File.Exists(protocolMatchPath), "Der alte Match-Partial muss entfernt bleiben.");
        var controller = File.ReadAllText(controllerPath);
        var training = File.ReadAllText(trainingPath);
        var acceptGreenCommandWorkflow = File.Exists(acceptGreenCommandWorkflowPath) ? File.ReadAllText(acceptGreenCommandWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var confirmWorkflow = File.Exists(confirmWorkflowPath) ? File.ReadAllText(confirmWorkflowPath) : "";
        var importTrainingResultWorkflow = File.Exists(importTrainingResultWorkflowPath) ? File.ReadAllText(importTrainingResultWorkflowPath) : "";
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);
        AssertNoForbiddenTokens(
            controller,
            "private async void CodingAcceptGreenMatches_Click",
            "private async void ImportConfirm_Click",
            "private async Task<bool> ConfirmImportAsTrainingAsync");
        AssertNoForbiddenTokens(
            training,
            "private async void CodingAcceptGreenMatches_Click",
            "private async void ImportConfirm_Click",
            "if (!_codingSessionHost.HasViewModel) return",
            "if (_lastCodingMatch == null)",
            "LstImportEvents.SelectedItem is not CodingEvent",
            "CodingProtocolImportTrainingWorkflowServiceFactory.Create",
            "new CodingProtocolImportTrainingConfirmationWorkflowActions",
            ".ConfirmAsync(importEvent)",
            "new CodingImportTrainingResultActions",
            "PlayerWindowTimerFactory.CreateOneShotTimer",
            "if (!result.Accepted)",
            "var badge = result.Badge",
            "TeacherAnnotationStore.AppendAsync",
            "LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation",
            "CodingProtocolTrainingSnapshotStoreFactory.Create");
        Assert.Contains("private void CodingAcceptGreenMatches_Click", training);
        Assert.Contains("private void ImportConfirm_Click", training);
        Assert.Contains(".SafeFireAndForget(\"CodingAcceptGreenMatches\")", training);
        Assert.Contains(".SafeFireAndForget(\"ImportConfirm\")", training);
        Assert.Contains("private async Task HandleCodingAcceptGreenMatchesAsync", training);
        Assert.Contains("RunProtocolMatch: () => _codingProtocolMatchController.RunMatch()", training);
        Assert.Contains("CodingAcceptGreenMatchesCommandWorkflow.ExecuteAsync", training);
        Assert.Contains("if (!request.HasCodingViewModel)", acceptGreenCommandWorkflow);
        Assert.Contains("actions.RunProtocolMatch()", acceptGreenCommandWorkflow);
        Assert.Contains("routing = actions.GetCurrentRouting()", acceptGreenCommandWorkflow);
        Assert.Contains("actions.AcceptGreenMatchesAsync(routing)", acceptGreenCommandWorkflow);
        Assert.Contains("actions.ShowOverlay(overlay.Value)", acceptGreenCommandWorkflow);
        Assert.Contains("private async Task HandleImportConfirmAsync", training);
        Assert.Contains("CodingImportConfirmCommandWorkflow.ExecuteAsync", training);
        Assert.Contains("request.SelectedItem is not CodingEvent", commandWorkflow);
        Assert.Contains("actions.ConfirmImportAsTrainingAsync(importEvent)", commandWorkflow);
        Assert.Contains("private async Task<bool> ConfirmImportAsTrainingAsync", training);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", confirmWorkflow);
        Assert.Contains("new CodingProtocolImportTrainingConfirmationWorkflowActions", confirmWorkflow);
        Assert.Contains("CodingProtocolImportTrainingConfirmationWorkflow.ConfirmAsync", training);
        Assert.Contains("_codingProtocolMatchController.SeekImportEvent", training);
        Assert.Contains("CodingProtocolGuidedVerificationAdapter.Create", training);
        Assert.Contains("_codingAiRuntimeOwner.Controller.ProtocolVerifier", training);
        Assert.Contains("service.ConfirmAsync(importEvent)", confirmWorkflow);
        Assert.Contains("CodingImportTrainingResultWorkflow.Execute", training);
        Assert.Contains("if (!importResult.Accepted)", importTrainingResultWorkflow);
        Assert.Contains("new CodingImportTrainingResultActions", importTrainingResultWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", importTrainingResultWorkflow);
        Assert.Contains("actions.ShowBadge(badge.Text)", importTrainingResultWorkflow);
        Assert.Contains("actions.ScheduleHideBadge(badge.AutoHideDelay)", importTrainingResultWorkflow);
        Assert.Contains("_codingSessionHost", training);
        Assert.Contains("CodingProtocolTrainingSnapshotStore", workflow);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation", workflowFactory);
        Assert.Contains("annotationStore.AppendAsync", workflowFactory);
    }

    [Fact]
    public void PlayerWindow_protocol_match_highlighting_lives_in_highlighting_partial()
    {
        var protocolMatchPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingProtocolMatchController.cs");
        var highlightingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Highlighting.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolMatchHighlightControls.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolMatchListHighlightWorkflow.cs");

        Assert.True(File.Exists(highlightingPath), "ProtocolMatch-Listenhighlighting soll aus dem Match-Partial heraus.");
        Assert.True(File.Exists(controlsPath), "ProtocolMatch-Listenhighlighting-Control-Zuweisung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "ProtocolMatch-Listenhighlighting-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        Assert.False(File.Exists(protocolMatchPath), "Der alte Match-Partial muss entfernt bleiben.");
        var controller = File.ReadAllText(controllerPath);
        var highlighting = File.ReadAllText(highlightingPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        AssertNoForbiddenTokens(
            controller,
            "private void ApplyCodingProtocolMatchListHighlights()",
            "private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)");
        Assert.Contains("private void ApplyCodingProtocolMatchListHighlights()", highlighting);
        Assert.Contains("private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)", highlighting);
        Assert.Contains("CodingProtocolMatchListHighlightWorkflow.Execute", highlighting);
        AssertNoForbiddenTokens(
            highlighting,
            "for (var i = 0; i < listBox.Items.Count; i++)",
            "CodingProtocolMatchDisplayPolicy.BackgroundColor",
            "CodingProtocolMatchDisplayPolicy.BadgeText",
            "badge.Visibility = Visibility.Visible",
            "emptyBadge.Visibility = Visibility.Collapsed");
        Assert.Contains("CodingProtocolMatchHighlightControls.Clear", highlighting);
        Assert.Contains("CodingProtocolMatchHighlightControls.Apply", highlighting);
        Assert.Contains("actions.HighlightItem(i)", workflow);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BackgroundColor", controls);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BadgeText", controls);
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
            "Verbotene alte Protocol-Match-Logik gefunden: " + string.Join(", ", hits));
    }
}
