using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingConfirmationArchitectureTests
{
    [Fact]
    public void PlayerWindow_confirmation_actions_use_workflows_and_delete_applier()
    {
        var confirmationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");
        var deleteApplierPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingEventDeleteApplier.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingConfirmationDecisionWorkflow.cs");
        var decisionCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingConfirmationDecisionCommandWorkflow.cs");
        var editCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingConfirmationEditCommandWorkflow.cs");

        Assert.True(File.Exists(deleteApplierPath), "Confirm-Reject muss die gemeinsame Coding-Event-Loeschanwendung nutzen.");
        Assert.True(File.Exists(workflowPath), "Confirm-Decision-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(decisionCommandWorkflowPath), "Confirm-Accept/Reject-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editCommandWorkflowPath), "Confirm-Edit-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var confirmation = File.ReadAllText(confirmationPath);
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var workflow = File.ReadAllText(workflowPath);
        var decisionCommandWorkflow = File.Exists(decisionCommandWorkflowPath) ? File.ReadAllText(decisionCommandWorkflowPath) : "";
        var editCommandWorkflow = File.Exists(editCommandWorkflowPath) ? File.ReadAllText(editCommandWorkflowPath) : "";

        Assert.Contains("CodingConfirmationDecisionWorkflow.Accept", confirmation);
        Assert.Contains("CodingConfirmationDecisionWorkflow.Edit", confirmation);
        Assert.Contains("CodingConfirmationDecisionWorkflow.Reject", confirmation);
        Assert.Contains("CodingConfirmationDecisionCommandWorkflow.Execute", confirmation);
        Assert.Contains("CodingConfirmationEditCommandWorkflow.Execute", confirmation);
        Assert.Contains("_codingSessionHost", confirmation);
        Assert.Contains("actions.ApplyDecision()", decisionCommandWorkflow);
        Assert.Contains("actions.CloseConfirmationPanel()", decisionCommandWorkflow);
        Assert.Contains("actions.ResumeAfterConfirmation()", decisionCommandWorkflow);
        Assert.Contains("CodingEventDecisionPolicy.ApplyAiConfirmationDecision", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("var selectedEvent = actions.EditConfirmation()", editCommandWorkflow);
        Assert.Contains("actions.CloseConfirmationPanel()", editCommandWorkflow);
        Assert.Contains("actions.SelectEvent(selectedEvent)", editCommandWorkflow);
        Assert.Contains("actions.ResumeAfterConfirmation()", editCommandWorkflow);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }

    [Fact]
    public void PlayerWindow_confirmation_panel_display_uses_controls_adapter()
    {
        var confirmationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingConfirmationPanelControls.cs");
        var ownerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingConfirmationPanelControlsOwner.cs");
        var initializerPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerCodingConfirmationPanelInitializer.cs");

        Assert.True(File.Exists(controlsPath), "Coding-Bestaetigungspanel-Anzeige soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(ownerPath), "Coding-Bestaetigungspanel-Besitz soll nicht als nullable Rohfeld im PlayerWindow liegen.");
        Assert.True(File.Exists(initializerPath), "Coding-Bestaetigungspanel-Control-Mapping soll ausserhalb der PlayerWindow-Partials liegen.");

        var confirmation = File.ReadAllText(confirmationPath);
        var state = File.ReadAllText(statePath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var initializer = File.Exists(initializerPath) ? File.ReadAllText(initializerPath) : "";

        Assert.Contains("private readonly CodingConfirmationPanelControlsOwner _codingConfirmationPanelControls = new();", state);
        Assert.Contains("PlayerCodingConfirmationPanelInitializer.Initialize", confirmation);
        Assert.Contains("new CodingConfirmationPanelControls(", initializer);
        Assert.Contains("_codingConfirmationPanelControls.Apply", confirmation);
        Assert.Contains("_codingConfirmationPanelControls.Hide()", confirmation);
        Assert.Contains("public sealed class CodingConfirmationPanelControls", controls);
        Assert.Contains("ConfirmAmpel.Fill", controls);
        Assert.Contains("CodingConfirmationPanel.Visibility = Visibility.Visible", controls);
        Assert.Contains("public sealed class CodingConfirmationPanelControlsOwner", owner);
        Assert.Contains("public void Initialize", owner);
        Assert.Contains("public Color Apply", owner);
        Assert.Contains("public void Hide", owner);
    }

    [Fact]
    public void PlayerWindow_confirmation_playback_uses_player_helper()
    {
        var helperPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerConfirmationPlayback.cs");
        var pauseWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingConfirmationPauseWorkflow.cs");
        var resumeWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingConfirmationResumeWorkflow.cs");
        var displayWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "LiveDetectionConfirmationDisplayWorkflow.cs");
        var codingConfirmationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");

        Assert.True(File.Exists(helperPath), "Confirmation-Playback-Regeln sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pauseWorkflowPath), "Coding-Confirmation-Pause-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resumeWorkflowPath), "Coding-Confirmation-Resume-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(displayWorkflowPath), "LiveDetection-Confirmation-Display-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");

        var helper = File.ReadAllText(helperPath);
        var pauseWorkflow = File.Exists(pauseWorkflowPath) ? File.ReadAllText(pauseWorkflowPath) : "";
        var resumeWorkflow = File.ReadAllText(resumeWorkflowPath);
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var codingConfirmation = File.ReadAllText(codingConfirmationPath);

        Assert.Contains("public static class PlayerConfirmationPlayback", helper);
        Assert.Contains("PauseCodingConfirmation", helper);
        Assert.Contains("ResumeCodingLiveAi", helper);
        Assert.Contains("PauseLiveDetectionConfirmation", helper);

        Assert.Contains("CodingConfirmationPauseWorkflow.Execute", codingConfirmation);
        Assert.Contains("PlayerConfirmationPlayback.PauseCodingConfirmation", pauseWorkflow);
        Assert.Contains("request.CodingSessionService?.SetWaitingForInput()", pauseWorkflow);
        Assert.Contains("actions.StorePendingConfirmation", pauseWorkflow);
        Assert.Contains("actions.ApplyConfirmationPanel", pauseWorkflow);
        Assert.Contains("CodingConfirmationResumeWorkflow.Apply", codingConfirmation);
        Assert.Contains("PlayerConfirmationPlayback.ResumeCodingLiveAi", resumeWorkflow);

        Assert.Contains("PlayerConfirmationPlayback.PauseLiveDetectionConfirmation", displayWorkflow);
    }
}
