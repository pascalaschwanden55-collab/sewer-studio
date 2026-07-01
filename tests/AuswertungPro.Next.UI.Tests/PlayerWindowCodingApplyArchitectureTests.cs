using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingApplyArchitectureTests
{
    [Fact]
    public void PlayerWindow_protocol_revision_update_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var applyPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolRevisionUpdater.cs");
        var updateBuilderPath = Path.Combine(uiRoot, "Ai", "CodingApplyProtocolUpdateBuilder.cs");
        var emptyGuardPath = Path.Combine(uiRoot, "Ai", "CodingApplyEmptyProtocolGuard.cs");
        var applyWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingApplyChangesWorkflow.cs");
        var closeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUnappliedChangesCloseWorkflow.cs");
        var emptyDialogWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingApplyEmptyProtocolDialogWorkflow.cs");
        var closeDialogWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUnappliedChangesCloseDialogWorkflow.cs");
        var closePolicyPath = Path.Combine(uiRoot, "Ai", "CodingUnappliedChangesClosePolicy.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingApplyDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingApplyDialogServiceFactory.cs");

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
        Assert.DoesNotContain("CodingProtocolRevisionUpdater.ApplyCodingEvents", apply);
        Assert.DoesNotContain("CodingApplyEmptyProtocolGuard.Build", apply);
        Assert.DoesNotContain("HasUnappliedCodingChanges", apply);
        Assert.DoesNotContain("CodingApplyDialogServiceFactory.Create", apply);
        Assert.DoesNotContain("new CodingApplyEmptyProtocolDialogWorkflowActions", apply);
        Assert.DoesNotContain("new CodingUnappliedChangesCloseDialogWorkflowActions", apply);
        Assert.Contains("_codingSessionHost", apply);
        Assert.Contains("ConfirmEmptyProtocol", apply);
        Assert.DoesNotContain(".ConfirmEmptyProtocol(", apply);
        Assert.DoesNotContain("ConfirmUnappliedChangesOnClose", apply);
        Assert.DoesNotContain("_codingVm", apply);
        Assert.DoesNotContain("new ProtocolDocument", apply);
        Assert.DoesNotContain("ProtocolRevisionCloner.CloneDocument", apply);
        Assert.DoesNotContain("doc.Current ??=", apply);
        Assert.DoesNotContain("_codingVm.Events.Count(", apply);
        Assert.DoesNotContain("DialogHost.Current", apply);
        Assert.DoesNotContain("CodingUnappliedChangesClosePolicy.ShouldClose", apply);
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
        Assert.DoesNotContain(".GroupBy(e => e.EntryId)", apply);
        Assert.DoesNotContain("aktiveBefunde", apply);
        Assert.DoesNotContain("bestehende(n) Befund", apply);
        Assert.DoesNotContain("result == DialogConfirm.Cancel", apply);
        Assert.DoesNotContain("result == DialogConfirm.Yes", apply);
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
