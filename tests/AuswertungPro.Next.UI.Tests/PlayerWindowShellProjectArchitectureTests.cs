using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowShellProjectArchitectureTests
{
    [Fact]
    public void PlayerWindow_shell_project_access_uses_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var applyControllerPath = Path.Combine(uiRoot, "Player", "CodingApplyController.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var previewWorkflowFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPreviewWorkflowServiceFactory.cs");
        var codingProjectPersistencePath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProjectPersistenceService.cs");
        var codingProjectPersistenceWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProjectPersistenceWorkflow.cs");
        var codingProjectPersistenceFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProjectPersistenceServiceFactory.cs");
        var servicePath = Path.Combine(uiRoot, "Player", "PlayerShellProjectService.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerShellProjectServiceFactory.cs");
        var shellPath = Path.Combine(uiRoot, "ViewModels", "ShellViewModel.cs");

        Assert.True(File.Exists(servicePath), "Shell-Projektzugriff soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "PlayerWindow soll Shell-Projektzugriff ueber eine Factory beziehen.");
        Assert.True(File.Exists(codingProjectPersistencePath), "Coding-Projektpersistenz soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingProjectPersistenceWorkflowPath), "Coding-Projektpersistenz-Aufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(codingProjectPersistenceFactoryPath), "Coding-Projektpersistenz soll ueber eine Factory verdrahtet werden.");

        var protocol = File.ReadAllText(protocolPath);
        var applyController = File.ReadAllText(applyControllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var previewWorkflowFactory = File.ReadAllText(previewWorkflowFactoryPath);
        var codingProjectPersistence = File.ReadAllText(codingProjectPersistencePath);
        var codingProjectPersistenceWorkflow = File.Exists(codingProjectPersistenceWorkflowPath) ? File.ReadAllText(codingProjectPersistenceWorkflowPath) : "";
        var codingProjectPersistenceFactory = File.ReadAllText(codingProjectPersistenceFactoryPath);
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var shell = File.ReadAllText(shellPath);

        Assert.Contains("PlayerShellProjectServiceFactory.Create", previewWorkflowFactory);
        Assert.Contains("CodingProjectPersistenceWorkflow.MarkProjectDirty", windowRoot);
        Assert.Contains("CodingProjectPersistenceWorkflow.TrySaveProjectIfReady", windowRoot);
        Assert.Contains("_bindings.MarkProjectDirty(_bindings.GetHaltungRecord())", applyController);
        Assert.Contains("SaveProjectAfterCoding: _bindings.SaveProjectAfterCoding", applyController);
        Assert.Contains("CodingProjectPersistenceServiceFactory.Create", codingProjectPersistenceWorkflow);
        Assert.Contains("new CodingProjectPersistenceWorkflowActions", codingProjectPersistenceWorkflow);
        Assert.Contains("service.MarkProjectDirty(record)", codingProjectPersistenceWorkflow);
        Assert.Contains("service.TrySaveProjectIfReady()", codingProjectPersistenceWorkflow);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", codingProjectPersistenceFactory);
        Assert.Contains("PlayerClock.UtcNow", codingProjectPersistenceFactory);
        Assert.Contains("ModifiedAtUtc", codingProjectPersistence);
        Assert.Contains("IPlayerShellProjectContext", service);
        Assert.Contains("IPlayerShellProjectContext", shell);
        Assert.Contains("App.Current", factory);

        var offenders = FindFileTokenOffenders(protocolPath, "App.Current")
            .Concat(FindFileTokenOffenders(applyControllerPath, "App.Current"))
            .Concat(FindFileTokenOffenders(windowRootPath, "App.Current"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Coding-Partials sollen Projektzugriff ueber PlayerShellProjectService kapseln:\n"
            + string.Join("\n", offenders));
    }
}
