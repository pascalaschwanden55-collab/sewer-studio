using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingImportArchitectureTests
{
    [Fact]
    public void PlayerWindow_import_reference_transfer_lives_in_policy()
    {
        var importPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Import.cs");
        var importActionsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ImportActions.cs");
        var codingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportReferenceTransfer.cs");
        var resetterPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSessionEventResetter.cs");
        var matchResetterPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolMatchStateResetter.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportReferenceInitializationWorkflow.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportReferenceControls.cs");
        var dropControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportReferenceDropController.cs");
        var confirmationControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportReferenceConfirmationController.cs");

        Assert.True(File.Exists(policyPath), "Import-Referenz-Transfer muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resetterPath), "Session-Event-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchResetterPath), "Protocol-Match-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Import-Referenz-Initialisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(controlsPath), "Import-Referenz-Zaehler sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(dropControllerPath), "Import-/KI-Drop-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(confirmationControllerPath), "Importbestaetigung soll ausserhalb der PlayerWindow-Partials liegen.");

        var import = File.ReadAllText(importPath);
        var importActions = File.ReadAllText(importActionsPath);
        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);
        var resetter = File.Exists(resetterPath) ? File.ReadAllText(resetterPath) : "";
        var matchResetter = File.Exists(matchResetterPath) ? File.ReadAllText(matchResetterPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var dropController = File.Exists(dropControllerPath) ? File.ReadAllText(dropControllerPath) : "";
        var confirmationController = File.Exists(confirmationControllerPath) ? File.ReadAllText(confirmationControllerPath) : "";

        Assert.Contains("CodingImportReferenceInitializationWorkflow.Execute", coding);
        Assert.Contains("CodingImportReferenceTransfer.MoveExistingEventsToImportReference", coding);
        Assert.Contains("CodingSessionEventResetter.ClearActiveSessionEvents", coding);
        Assert.Contains("_codingProtocolMatchState.Reset", coding);
        Assert.Contains("_codingSessionHost", coding);
        Assert.Contains("CodingImportReferenceControls.SetCount", import);
        Assert.Contains("_codingImportReferenceConfirmationController.ExecuteAsync", importActions);
        Assert.DoesNotContain("CodingEventDecisionPolicy.ApplyManualReviewDecision", importActions);
        Assert.Contains("CodingImportReferenceControls.SetCount", coding);
        Assert.Contains("_codingImportReferenceDropController.Execute", coding);
        Assert.DoesNotContain("CodingEventColumnTransfer.CloneWithNewIds", coding);
        Assert.DoesNotContain("CodingEventColumnTransfer.Move(ev", coding);
        Assert.Contains("public static int MoveExistingEventsToImportReference", policy);
        Assert.Contains("public static int ClearActiveSessionEvents", resetter);
        Assert.Contains("public static CodingMatchRouting? Reset", matchResetter);
        Assert.Contains("actions.ResetProtocolMatchState()", workflow);
        Assert.Contains("actions.UpdateProtocolMatchSummary(matchRouting)", workflow);
        Assert.Contains("actions.MoveExistingEventsToImportReference()", workflow);
        Assert.Contains("actions.SetImportCount(importEventCount)", workflow);
        Assert.Contains("actions.ClearActiveSessionEvents()", workflow);
        Assert.Contains("actions.SetCodingCount(0)", workflow);
        Assert.Contains("actions.BuildBaselineSignature()", workflow);
        Assert.Contains("actions.SetBaselineSignature(baselineSignature)", workflow);
        Assert.Contains("actions.ResetStretchTracker()", workflow);
        Assert.Contains("public static void SetCount", controls);
        Assert.Contains("public sealed class CodingImportReferenceDropController", dropController);
        Assert.Contains("public sealed class CodingImportReferenceConfirmationController", confirmationController);
    }
}
