using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

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
        var seekBody = ExtractMethodBody(protocolMatch, "private void SeekToImportEvent(object? selectedItem)");
        var runBody = ExtractMethodBody(protocolMatch, "private void RunCodingProtocolMatch()");

        Assert.Contains("CodingImportEventSeekCommandWorkflow.Execute", seekBody);
        Assert.Contains("CodingProtocolMatchCommandWorkflow.Execute", runBody);
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
}
