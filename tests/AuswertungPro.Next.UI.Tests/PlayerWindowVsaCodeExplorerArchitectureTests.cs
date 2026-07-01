using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowVsaCodeExplorerArchitectureTests
{
    [Fact]
    public void PlayerWindow_vsa_code_explorer_window_creation_lives_in_dialog_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var servicePath = Path.Combine(uiRoot, "Services", "VsaCodeExplorerDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Services", "VsaCodeExplorerDialogServiceFactory.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowServiceFactory.cs");
        var serviceCreationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerServiceCreationWorkflow.cs");

        Assert.True(File.Exists(servicePath), "VSA-Code-Explorer-Dialoggrenze muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "VSA-Code-Explorer-Fenstererzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Code-Explorer-Workflow muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "Coding-Code-Explorer-Workflow muss ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(serviceCreationWorkflowPath), "Coding-Code-Explorer-Serviceerstellung soll ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var codeExplorerDialog = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Coding.CodeExplorer.Dialog.cs"));
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);
        var serviceCreationWorkflow = File.Exists(serviceCreationWorkflowPath) ? File.ReadAllText(serviceCreationWorkflowPath) : "";

        Assert.DoesNotContain("VsaCodeExplorerDialogServiceFactory.Create", playerWindowText);
        Assert.DoesNotContain("CodingCodeExplorerWorkflowServiceFactory.Create", playerWindowText);
        Assert.Contains("CodingCodeExplorerServiceCreationWorkflow.Create", codeExplorerDialog);
        Assert.Contains("CreateVsaCodeExplorerLiveSnapshotProvider", playerWindowText);
        Assert.DoesNotContain("new VsaCodeExplorerWindow", playerWindowText);
        Assert.DoesNotContain("new Views.Windows.VsaCodeExplorerWindow", playerWindowText);
        Assert.Contains("public sealed record VsaCodeExplorerDialogRequest", service);
        Assert.Contains("public sealed record VsaCodeExplorerDialogResult", service);
        Assert.Contains("new VsaCodeExplorerWindow", factory);
        Assert.Contains("LiveSnapshotProvider", factory);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", workflow);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", workflowFactory);
        Assert.Contains("CodingCodeExplorerWorkflowServiceFactory.Create", serviceCreationWorkflow);
        Assert.Contains("actions.CreateService(createViewModel)", serviceCreationWorkflow);
    }
}
