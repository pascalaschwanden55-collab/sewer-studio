using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowExplorerEntryEditArchitectureTests
{
    [Fact]
    public void PlayerWindow_explorer_entry_edits_use_copier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var eventActionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var detailsActionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs");
        var markCatalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowService.cs");
        var editWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerEditWorkflow.cs");
        var copierPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEntryCopier.cs");

        Assert.True(File.Exists(workflowPath), "Code-Explorer-Workflow soll editierbare Werte ausserhalb der PlayerWindow-Partials kopieren.");
        Assert.True(File.Exists(editWorkflowPath), "Code-Explorer-Edit-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var events = File.ReadAllText(eventsPath);
        var eventActions = File.ReadAllText(eventActionsPath);
        var detailsActions = File.ReadAllText(detailsActionsPath);
        var markCatalog = File.ReadAllText(markCatalogPath);
        var workflow = File.ReadAllText(workflowPath);
        var editWorkflow = File.Exists(editWorkflowPath) ? File.ReadAllText(editWorkflowPath) : "";
        var copier = File.ReadAllText(copierPath);

        Assert.Contains("CodingCodeExplorerEditWorkflow.Execute", eventActions);
        Assert.Contains("CodingCodeExplorerEditWorkflow.Execute", detailsActions);
        Assert.Contains(".TryEdit(", editWorkflow);
        Assert.Contains("CodingProtocolEntryCopier.CopyEditableValues", workflow);
        Assert.Contains("public static void CopyEditableValues", copier);
    }
}
