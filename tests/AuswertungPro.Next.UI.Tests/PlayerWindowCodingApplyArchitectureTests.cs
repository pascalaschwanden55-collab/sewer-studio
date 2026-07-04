using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingApplyArchitectureTests
{
    [Fact]
    public void PlayerWindow_protocol_revision_update_lives_in_policy()
    {
        var applyPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolRevisionUpdater.cs");
        var updateBuilderPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingApplyProtocolUpdateBuilder.cs");
        var emptyGuardPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingApplyEmptyProtocolGuard.cs");
        var applyWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingApplyChangesWorkflow.cs");
        var closeWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingUnappliedChangesCloseWorkflow.cs");
        var emptyDialogWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingApplyEmptyProtocolDialogWorkflow.cs");
        var closeDialogWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingUnappliedChangesCloseDialogWorkflow.cs");
        var closePolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingUnappliedChangesClosePolicy.cs");
        var dialogServicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingApplyDialogService.cs");
        var dialogServiceFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingApplyDialogServiceFactory.cs");

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

        var apply = File.ReadAllText(applyPath);
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
        Assert.Contains("CodingApplyChangesWorkflow.Execute", apply);
        Assert.Contains("CodingUnappliedChangesCloseWorkflow.Execute", apply);
        Assert.Contains("CodingApplyEmptyProtocolDialogWorkflow.Execute", apply);
        Assert.Contains("CodingUnappliedChangesCloseDialogWorkflow.Execute", apply);
        Assert.Contains("_codingSessionHost", apply);
        Assert.Contains("ConfirmEmptyProtocol", apply);
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
