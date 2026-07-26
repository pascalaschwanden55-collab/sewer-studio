using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingApplyArchitectureTests
{
    [Fact]
    public void Coding_apply_and_close_lifecycle_lives_in_controller()
    {
        var applyPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingApplyController.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var windowStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.State.cs");
        var codingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs");
        var lifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.Lifecycle.cs");
        var protocolPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingProtocolRevisionUpdater.cs");
        var updateBuilderPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingApplyProtocolUpdateBuilder.cs");
        var emptyGuardPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingApplyEmptyProtocolGuard.cs");
        var applyWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingApplyChangesWorkflow.cs");
        var closeWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingUnappliedChangesCloseWorkflow.cs");
        var emptyDialogWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingApplyEmptyProtocolDialogWorkflow.cs");
        var closeDialogWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingUnappliedChangesCloseDialogWorkflow.cs");
        var closePolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingUnappliedChangesClosePolicy.cs");
        var dialogServicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingApplyDialogService.cs");
        var dialogServiceFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingApplyDialogServiceFactory.cs");

        Assert.False(File.Exists(applyPath), "Der uebernommene Codierungsablauf darf nicht wieder als PlayerWindow-Partial erscheinen.");
        Assert.True(File.Exists(controllerPath), "Uebernehmen und Schliessschutz brauchen einen eigenen Controller.");
        Assert.True(File.Exists(policyPath), "Protokoll-Revision-Update muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(updateBuilderPath), "Protokoll-Dokumentvorbereitung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(emptyGuardPath), "Leere-Codierung-Schutzlogik muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applyWorkflowPath), "ApplyCodingChanges-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(closeWorkflowPath), "Unuebernommene-Codierungen-Schliessen-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(emptyDialogWorkflowPath), "Leere-Codierung-Dialog soll ausserhalb der PlayerWindow-Partials ausgefuehrt werden.");
        Assert.True(File.Exists(closeDialogWorkflowPath), "Unuebernommene-Codierungen-Schliessen-Dialog soll ausserhalb der PlayerWindow-Partials ausgefuehrt werden.");
        Assert.True(File.Exists(closePolicyPath), "Schliessen-Entscheidung fuer unuebernommene Codierungen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Apply-Dialogtexte und DialogHost-Zugriff muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Apply-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");

        var controller = File.ReadAllText(controllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var windowState = File.ReadAllText(windowStatePath);
        var coding = File.ReadAllText(codingPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var updateBuilder = File.ReadAllText(updateBuilderPath);
        var emptyGuard = File.ReadAllText(emptyGuardPath);
        var applyWorkflow = File.Exists(applyWorkflowPath) ? File.ReadAllText(applyWorkflowPath) : "";
        var closeWorkflow = File.Exists(closeWorkflowPath) ? File.ReadAllText(closeWorkflowPath) : "";
        var emptyDialogWorkflow = File.Exists(emptyDialogWorkflowPath) ? File.ReadAllText(emptyDialogWorkflowPath) : "";
        var closeDialogWorkflow = File.Exists(closeDialogWorkflowPath) ? File.ReadAllText(closeDialogWorkflowPath) : "";
        var closePolicy = File.ReadAllText(closePolicyPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);

        Assert.Contains("CodingApplyProtocolUpdateBuilder.Create", applyWorkflow);
        Assert.Contains("public interface ICodingApplyController", controller);
        Assert.Contains("public sealed class CodingApplyController", controller);
        Assert.Contains("CodingApplyChangesWorkflow.Execute", controller);
        Assert.Contains("CodingUnappliedChangesCloseWorkflow.Execute", controller);
        Assert.Contains("_bindings.ConfirmEmptyProtocol", controller);
        Assert.Contains("_bindings.ConfirmUnappliedChanges(() => Apply(showOverlay: false))", controller);
        Assert.Contains("private readonly ICodingApplyController _codingApplyController", windowState);
        Assert.Contains("new CodingApplyController(", windowRoot);
        Assert.Contains("_codingSessionHost", windowRoot);
        Assert.Contains("CodingApplyEmptyProtocolDialogWorkflow.Execute", windowRoot);
        Assert.Contains("CodingUnappliedChangesCloseDialogWorkflow.Execute", windowRoot);
        Assert.Contains("CodingProjectPersistenceWorkflow.MarkProjectDirty", windowRoot);
        Assert.Contains("CodingProjectPersistenceWorkflow.TrySaveProjectIfReady", windowRoot);
        Assert.Contains("_codingApplyController.Apply(showOverlay: true)", coding);
        Assert.Contains("ConfirmCanClose: _codingApplyController.ConfirmCanClose", lifecycle);
        Assert.Contains("_codingApplyController.MarkProjectDirty", protocol);
        Assert.DoesNotContain("private bool ApplyCodingChanges", windowRoot);
        Assert.DoesNotContain("private bool ConfirmUnappliedCodingChangesOnClose", windowRoot);
        Assert.Contains("CodingProtocolRevisionUpdater.ApplyCodingEvents", applyWorkflow);
        Assert.Contains("CodingApplyEmptyProtocolGuard.Build", applyWorkflow);
        Assert.Contains("actions.AssignProtocol(update.Document)", applyWorkflow);
        Assert.Contains("actions.SyncCodingToPrimaryDamages(update.Document)", applyWorkflow);
        Assert.Contains("actions.SetBaselineSignature", applyWorkflow);
        Assert.Contains("actions.BuildSignature(request.Events)", closeWorkflow);
        Assert.Contains("actions.ConfirmWithSuspendedOverlay()", closeWorkflow);
        Assert.Contains("CodingApplyDialogServiceFactory.Create", emptyDialogWorkflow);
        Assert.Contains("new CodingApplyEmptyProtocolDialogWorkflowActions", emptyDialogWorkflow);
        Assert.Contains("actions.CreateDialogService()", emptyDialogWorkflow);
        Assert.Contains("ConfirmEmptyProtocol", emptyDialogWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlay", closeDialogWorkflow);
        Assert.Contains("CodingApplyDialogServiceFactory.Create", closeDialogWorkflow);
        Assert.Contains("new CodingUnappliedChangesCloseDialogWorkflowActions", closeDialogWorkflow);
        Assert.Contains("actions.CreateDialogService()", closeDialogWorkflow);
        Assert.Contains("ConfirmUnappliedChangesOnClose", closeDialogWorkflow);
        Assert.Contains("public static int ApplyCodingEvents", policy);
        Assert.Contains("public static CodingApplyProtocolUpdate Create", updateBuilder);
        Assert.Contains("public static CodingApplyEmptyProtocolGuardResult Build", emptyGuard);
        Assert.Contains("public static bool ShouldClose", closePolicy);
        Assert.Contains("public sealed class CodingApplyDialogService", dialogService);
        Assert.Contains("_confirmWarn", dialogService);
        Assert.Contains("_confirmCancel", dialogService);
        Assert.Contains("CodingUnappliedChangesClosePolicy.ShouldClose", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("ConfirmWarn", dialogServiceFactory);
        Assert.Contains("ConfirmCancel", dialogServiceFactory);
    }
}
