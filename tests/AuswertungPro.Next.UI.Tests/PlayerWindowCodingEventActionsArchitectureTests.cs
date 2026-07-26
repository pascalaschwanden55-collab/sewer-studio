using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingEventActionsArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_event_actions_live_in_actions_partial()
    {
        var actionsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Events.Actions.cs");
        var dialogServicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventActionDialogService.cs");
        var dialogServiceFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventActionDialogServiceFactory.cs");
        var dialogWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventActionDialogWorkflow.cs");
        var deleteApplierPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventDeleteApplier.cs");
        var editApplierPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventEditApplier.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventListActionWorkflow.cs");
        var seekCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventSeekCommandWorkflow.cs");
        var editCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventEditCommandWorkflow.cs");
        var editButtonCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventEditButtonCommandWorkflow.cs");
        var deleteCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventDeleteCommandWorkflow.cs");
        var closeStretchCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventCloseStretchCommandWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionshandler sollen aus dem allgemeinen Events-Partial heraus.");
        Assert.True(File.Exists(dialogServicePath), "Coding-Event-Aktionsdialoge muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Coding-Event-Aktionsdialog-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogWorkflowPath), "Coding-Event-Aktionsdialog-Aufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(deleteApplierPath), "Coding-Event-Loeschanwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(editApplierPath), "Coding-Event-Bearbeitungsanwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Event-Listenaktionen sollen die Apply/Delete-Nachbearbeitung ausserhalb der PlayerWindow-Partials kapseln.");
        Assert.True(File.Exists(seekCommandWorkflowPath), "Coding-Event-Seek-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editCommandWorkflowPath), "Coding-Event-Edit-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editButtonCommandWorkflowPath), "Coding-Event-Edit-Button-Auswahlguard soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(deleteCommandWorkflowPath), "Coding-Event-Delete-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(closeStretchCommandWorkflowPath), "Coding-Event-Streckenschaden-Schliessen soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var actions = File.ReadAllText(actionsPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var dialogWorkflow = File.Exists(dialogWorkflowPath) ? File.ReadAllText(dialogWorkflowPath) : "";
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var editApplier = File.ReadAllText(editApplierPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var seekCommandWorkflow = File.Exists(seekCommandWorkflowPath) ? File.ReadAllText(seekCommandWorkflowPath) : "";
        var editCommandWorkflow = File.Exists(editCommandWorkflowPath) ? File.ReadAllText(editCommandWorkflowPath) : "";
        var editButtonCommandWorkflow = File.Exists(editButtonCommandWorkflowPath) ? File.ReadAllText(editButtonCommandWorkflowPath) : "";
        var deleteCommandWorkflow = File.Exists(deleteCommandWorkflowPath) ? File.ReadAllText(deleteCommandWorkflowPath) : "";
        var closeStretchCommandWorkflow = File.Exists(closeStretchCommandWorkflowPath) ? File.ReadAllText(closeStretchCommandWorkflowPath) : "";

        Assert.Contains("private void CodingEvents_DoubleClick", actions);
        Assert.Contains("private void CodingEventEdit_Click", actions);
        Assert.Contains("private void CodingEventSeek_Click", actions);
        Assert.Contains("private void CodingEventCloseStretch_Click", actions);
        Assert.Contains("private void CodingEventDelete_Click", actions);
        Assert.Contains("CodingEventActionDialogWorkflow.ShowStretchCloseRequiresLaterMeter", actions);
        Assert.Contains("CodingEventActionDialogWorkflow.ConfirmDelete", actions);
        Assert.Contains("CodingEventSeekCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventEditCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventEditButtonCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventDeleteCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventCloseStretchCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventListActionWorkflow.CompleteEdit", actions);
        Assert.Contains("CodingEventListActionWorkflow.CloseStretch", actions);
        Assert.Contains("CodingEventListActionWorkflow.Delete", actions);
        Assert.Contains("_codingSessionHost", actions);
        Assert.Contains("CodingEventSeekPolicy.TryGetSeekMilliseconds", seekCommandWorkflow);
        Assert.Contains("actions.SeekMilliseconds(milliseconds)", seekCommandWorkflow);
        Assert.Contains("actions.PausePlayback()", editCommandWorkflow);
        Assert.Contains("actions.TryEdit(selectedEvent)", editCommandWorkflow);
        Assert.Contains("actions.CompleteEdit(selectedEvent)", editCommandWorkflow);
        Assert.Contains("request.SelectedItem is not CodingEvent", editButtonCommandWorkflow);
        Assert.Contains("actions.EditSelectedEvent(selectedEvent)", editButtonCommandWorkflow);
        Assert.Contains("actions.ConfirmDelete(selectedEvent.Entry.Code)", deleteCommandWorkflow);
        Assert.Contains("actions.Delete(selectedEvent)", deleteCommandWorkflow);
        Assert.Contains("actions.HideInlineDefectDetail()", deleteCommandWorkflow);
        Assert.Contains("actions.RefreshEvents()", deleteCommandWorkflow);
        Assert.Contains("actions.CloseStretch(selectedEvent)", closeStretchCommandWorkflow);
        Assert.Contains("actions.ShowRequiresLaterMeterPrompt()", closeStretchCommandWorkflow);
        Assert.Contains("actions.RefreshEvents()", closeStretchCommandWorkflow);
        Assert.Contains("actions.ShowSuccessStatus(closeAction.StatusText)", closeStretchCommandWorkflow);
        Assert.Contains("CodingEventEditApplier.Apply", workflow);
        Assert.Contains("CodingStretchDamageManualCloseApplier.Apply", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("CodingEventActionDialogServiceFactory.Create", dialogWorkflow);
        Assert.Contains("new CodingEventActionDialogWorkflowActions", dialogWorkflow);
        Assert.Contains("service.ShowStretchCloseRequiresLaterMeter()", dialogWorkflow);
        Assert.Contains("service.ConfirmDelete(code)", dialogWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlay", dialogWorkflow);
        Assert.Contains("ShowStretchCloseRequiresLaterMeter", dialogService);
        Assert.Contains("ConfirmDelete", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("codingSessionService?.UpdateEvent", editApplier);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }
}
