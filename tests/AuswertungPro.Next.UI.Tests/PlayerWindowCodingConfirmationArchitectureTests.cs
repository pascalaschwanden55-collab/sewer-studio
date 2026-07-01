using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingConfirmationArchitectureTests
{
    [Fact]
    public void PlayerWindow_confirmation_actions_use_workflows_and_delete_applier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var confirmationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationDecisionWorkflow.cs");
        var decisionCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationDecisionCommandWorkflow.cs");
        var editCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationEditCommandWorkflow.cs");

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
        Assert.DoesNotContain("CloseConfirmationAndResume();", confirmation);
        Assert.DoesNotContain("if (selectedEvent != null)", confirmation);
        Assert.DoesNotContain("var selectedEvent = CodingConfirmationDecisionWorkflow.Edit", confirmation);
        Assert.DoesNotContain("CodingEventDecisionPolicy.ApplyAiConfirmationDecision", confirmation);
        Assert.DoesNotContain("CodingEventDeleteApplier.Apply", confirmation);
        Assert.Contains("_codingSessionHost", confirmation);
        Assert.DoesNotContain("_codingVm", confirmation);
        Assert.DoesNotContain("_codingSessionService?.RemoveEvent", confirmation);
        Assert.DoesNotContain("_codingVm?.Events.Remove", confirmation);
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
}
