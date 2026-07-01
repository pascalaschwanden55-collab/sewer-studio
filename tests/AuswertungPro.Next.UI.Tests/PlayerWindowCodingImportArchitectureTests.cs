using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingImportArchitectureTests
{
    [Fact]
    public void PlayerWindow_import_reference_transfer_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var importPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Import.cs");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceTransfer.cs");
        var resetterPath = Path.Combine(uiRoot, "Ai", "CodingSessionEventResetter.cs");
        var matchResetterPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchStateResetter.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceInitializationWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceControls.cs");

        Assert.True(File.Exists(policyPath), "Import-Referenz-Transfer muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resetterPath), "Session-Event-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchResetterPath), "Protocol-Match-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Import-Referenz-Initialisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(controlsPath), "Import-Referenz-Zaehler sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var import = File.ReadAllText(importPath);
        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);
        var resetter = File.Exists(resetterPath) ? File.ReadAllText(resetterPath) : "";
        var matchResetter = File.Exists(matchResetterPath) ? File.ReadAllText(matchResetterPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingImportReferenceInitializationWorkflow.Execute", coding);
        Assert.Contains("CodingImportReferenceTransfer.MoveExistingEventsToImportReference", coding);
        Assert.Contains("CodingSessionEventResetter.ClearActiveSessionEvents", coding);
        Assert.Contains("_codingProtocolMatchState.Reset", coding);
        Assert.Contains("_codingSessionHost", coding);
        Assert.DoesNotContain("_codingVm", coding);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel)", coding);
        Assert.DoesNotContain("if (eventCollection is null)", coding);
        Assert.Contains("CodingImportReferenceControls.SetCount", import);
        Assert.Contains("CodingImportReferenceControls.SetCount", coding);
        Assert.DoesNotContain("RunImportDefectCount.Text", import + coding);
        Assert.DoesNotContain("RunCodingDefectCount.Text", coding);
        Assert.DoesNotContain("_lastCodingMatch = null", coding);
        Assert.DoesNotContain("_codingProtocolMatchBuckets.Clear()", coding);
        Assert.DoesNotContain("ActiveSession?.Events.Clear", coding);
        Assert.DoesNotContain("var allExisting = _codingVm.Events.OrderBy", coding);
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
    }
}
