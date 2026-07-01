using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowShellProjectArchitectureTests
{
    [Fact]
    public void PlayerWindow_shell_project_access_uses_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var applyPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var previewWorkflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWorkflowServiceFactory.cs");
        var codingProjectPersistencePath = Path.Combine(uiRoot, "Ai", "CodingProjectPersistenceService.cs");
        var codingProjectPersistenceWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProjectPersistenceWorkflow.cs");
        var codingProjectPersistenceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProjectPersistenceServiceFactory.cs");
        var servicePath = Path.Combine(uiRoot, "Player", "PlayerShellProjectService.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerShellProjectServiceFactory.cs");
        var shellPath = Path.Combine(uiRoot, "ViewModels", "ShellViewModel.cs");

        Assert.True(File.Exists(servicePath), "Shell-Projektzugriff soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "PlayerWindow soll Shell-Projektzugriff ueber eine Factory beziehen.");
        Assert.True(File.Exists(codingProjectPersistencePath), "Coding-Projektpersistenz soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingProjectPersistenceWorkflowPath), "Coding-Projektpersistenz-Aufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(codingProjectPersistenceFactoryPath), "Coding-Projektpersistenz soll ueber eine Factory verdrahtet werden.");

        var protocol = File.ReadAllText(protocolPath);
        var apply = File.ReadAllText(applyPath);
        var previewWorkflowFactory = File.ReadAllText(previewWorkflowFactoryPath);
        var codingProjectPersistence = File.ReadAllText(codingProjectPersistencePath);
        var codingProjectPersistenceWorkflow = File.Exists(codingProjectPersistenceWorkflowPath) ? File.ReadAllText(codingProjectPersistenceWorkflowPath) : "";
        var codingProjectPersistenceFactory = File.ReadAllText(codingProjectPersistenceFactoryPath);
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var shell = File.ReadAllText(shellPath);

        Assert.DoesNotContain("PlayerShellProjectServiceFactory.Create", protocol);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", previewWorkflowFactory);
        Assert.DoesNotContain("PlayerShellProjectServiceFactory.Create", apply);
        Assert.DoesNotContain("CodingProjectPersistenceServiceFactory.Create", apply);
        Assert.DoesNotContain("new CodingProjectPersistenceWorkflowActions", apply);
        Assert.Contains("CodingProjectPersistenceWorkflow.MarkProjectDirty", apply);
        Assert.Contains("CodingProjectPersistenceWorkflow.TrySaveProjectIfReady", apply);
        Assert.Contains("CodingProjectPersistenceWorkflow.MarkProjectDirty(_protocolContext.HaltungRecord)", apply);
        Assert.Contains("CodingProjectPersistenceWorkflow.TrySaveProjectIfReady()", apply);
        Assert.Contains("CodingProjectPersistenceServiceFactory.Create", codingProjectPersistenceWorkflow);
        Assert.Contains("new CodingProjectPersistenceWorkflowActions", codingProjectPersistenceWorkflow);
        Assert.Contains("service.MarkProjectDirty(record)", codingProjectPersistenceWorkflow);
        Assert.Contains("service.TrySaveProjectIfReady()", codingProjectPersistenceWorkflow);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", codingProjectPersistenceFactory);
        Assert.Contains("PlayerClock.UtcNow", codingProjectPersistenceFactory);
        Assert.Contains("ModifiedAtUtc", codingProjectPersistence);
        Assert.DoesNotContain("App.Current", protocol + apply);
        Assert.Contains("IPlayerShellProjectContext", service);
        Assert.Contains("IPlayerShellProjectContext", shell);
        Assert.Contains("App.Current", factory);
    }
}
