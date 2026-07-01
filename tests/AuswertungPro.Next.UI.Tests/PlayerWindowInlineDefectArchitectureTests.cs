using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowInlineDefectArchitectureTests
{
    [Fact]
    public void PlayerWindow_inline_defect_detail_uses_display_policy_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingDefectStatusDisplayPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectDetailControls.cs");
        var selectionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectSelectionWorkflow.cs");

        var detail = File.ReadAllText(detailPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var selectionWorkflow = File.Exists(selectionWorkflowPath) ? File.ReadAllText(selectionWorkflowPath) : "";

        Assert.True(File.Exists(controlsPath), "Inline-Defekt-Detail-Control-Mapping soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(selectionWorkflowPath), "Inline-Defekt-Auswahlentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.Contains("CodingInlineDefectSelectionWorkflow.Execute", detail);
        Assert.Contains("new CodingInlineDefectSelectionActions", detail);
        Assert.Contains("_codingSessionHost", detail);
        Assert.Contains("CodingDefectStatusDisplayPolicy.BuildInlineDetail", detail);
        Assert.Contains("_codingSidePanelControllers.InlineDefectDetail.Apply(state)", detail);
        Assert.Contains("_codingSidePanelControllers.InlineDefectDetail.Hide()", detail);
        Assert.Contains("actions.UpdateInlineDefectDetail(selectedEvent)", selectionWorkflow);
        Assert.Contains("actions.HideInlineDefectDetail()", selectionWorkflow);
        Assert.DoesNotContain("_codingVm", detail);
        Assert.DoesNotContain("if (selection.SelectedEvent is not null)", detail);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is CodingEvent", detail);
        Assert.DoesNotContain("_codingVm.SelectedDefect = ev", detail);
        Assert.DoesNotContain("_codingVm.SelectedDefect = null", detail);
        Assert.DoesNotContain("TxtInlineDetailCode.Text = state.CodeText", detail);
        Assert.DoesNotContain("BtnInlineAccept.Visibility = state.CanAct", detail);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Source = null", detail);
        Assert.DoesNotContain("$\"{ev.MeterAtCapture:F2}m\"", detail);
        Assert.DoesNotContain("$\"{conf * 100:F0}%\"", detail);
        Assert.Contains("public static CodingInlineDefectDetailState BuildInlineDetail", policy);
        Assert.Contains("TxtInlineDetailCode.Text = state.CodeText", controls);
        Assert.Contains("BtnInlineAccept.Visibility = state.CanAct", controls);
        Assert.Contains("ImgInlineEvidencePreview.Source = null", controls);
        Assert.Contains("public static CodingInlineDefectSelectionResult Apply", selectionWorkflow);
        Assert.Contains("public static CodingInlineDefectSelectionWorkflowResult Execute", selectionWorkflow);
        Assert.Contains("actions.UpdateInlineDefectDetail(selectedEvent)", selectionWorkflow);
        Assert.Contains("actions.HideInlineDefectDetail()", selectionWorkflow);
    }

    [Fact]
    public void PlayerWindow_inline_defect_preview_lives_in_preview_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var previewPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Preview.cs");
        var previewServicePath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewService.cs");
        var previewWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewWorkflow.cs");

        Assert.True(File.Exists(previewPath), "Inline-Defekt-Bildvorschau soll in einem eigenen EventDetails-Partial liegen.");
        Assert.True(File.Exists(previewServicePath), "Inline-Defekt-Bildvorschau soll Datei- und Bitmap-Logik auslagern.");
        Assert.True(File.Exists(previewWorkflowPath), "Inline-Defekt-Bildvorschau-Fehlerbehandlung soll ausserhalb von PlayerWindow liegen.");

        var detail = File.ReadAllText(detailPath);
        var preview = File.ReadAllText(previewPath);
        var previewService = File.ReadAllText(previewServicePath);
        var previewWorkflow = File.ReadAllText(previewWorkflowPath);

        Assert.Contains("UpdateInlineEvidencePreview(ev);", detail);
        Assert.DoesNotContain("private void UpdateInlineEvidencePreview", detail);
        Assert.DoesNotContain("CodingDefectPreviewService.BuildPreviewImagePath", detail);
        Assert.DoesNotContain("BitmapImage", detail);
        Assert.Contains("private void UpdateInlineEvidencePreview", preview);
        Assert.Contains("CodingInlineEvidencePreviewWorkflow.Execute", preview);
        Assert.DoesNotContain("CodingInlineEvidencePreviewService.Build", preview);
        Assert.DoesNotContain("catch (Exception", preview);
        Assert.Contains("CodingInlineEvidencePreviewService.Build", previewWorkflow);
        Assert.Contains("CodingInlineEvidencePreviewService.LoadFailed", previewWorkflow);
        Assert.Contains("_codingSidePanelControllers.InlineDefectDetail.ApplyPreview", preview);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Source = state.Source", preview);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Visibility = state.ImageVisible", preview);
        Assert.DoesNotContain("TxtInlineEvidencePreviewStatus.Text = state.StatusText", preview);
        Assert.DoesNotContain("TxtInlineEvidencePreviewStatus.Visibility = state.StatusVisible", preview);
        Assert.Contains("public void ApplyPreview", File.ReadAllText(Path.Combine(uiRoot, "Ai", "CodingInlineDefectDetailControls.cs")));
        Assert.DoesNotContain("CodingDefectPreviewService.BuildPreviewImagePath", preview);
        Assert.DoesNotContain("BitmapImage", preview);
        Assert.Contains("CodingDefectPreviewService.BuildPreviewImagePath", previewService);
        Assert.Contains("BitmapImage", previewService);
    }

    [Fact]
    public void PlayerWindow_event_list_right_click_selection_uses_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var helperPath = Path.Combine(uiRoot, "Ai", "CodingEventListItemSelectionHelper.cs");

        Assert.True(File.Exists(helperPath), "Eventlisten-Rechtsklick-Auswahl soll ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var helper = File.Exists(helperPath) ? File.ReadAllText(helperPath) : "";

        Assert.Contains("CodingEventListItemSelectionHelper.SelectContainingListBoxItem", detail);
        Assert.DoesNotContain("while (dep != null && dep is not ListBoxItem)", detail);
        Assert.DoesNotContain("VisualTreeHelper.GetParent(dep)", detail);
        Assert.Contains("public static bool SelectContainingListBoxItem", helper);
        Assert.Contains("VisualTreeHelper.GetParent", helper);
        Assert.Contains("LogicalTreeHelper.GetParent", helper);
    }

    [Fact]
    public void PlayerWindow_coding_event_list_item_coloring_lives_in_list_items_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var listItemsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.ListItems.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventListItemColorizeWorkflow.cs");

        Assert.True(File.Exists(listItemsPath), "Event-ListBox-Einfaerbung soll aus dem Inline-Detail-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Event-ListBox-Einfaerbungsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var listItems = File.ReadAllText(listItemsPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("private void ColorizeCodingEventListItems", detail);
        Assert.DoesNotContain("\"ZoneDot\"", detail);
        Assert.DoesNotContain("\"TxtConfidence\"", detail);
        Assert.Contains("private void ColorizeCodingEventListItems", listItems);
        Assert.Contains("CodingEventListItemColorizeWorkflow.Execute", listItems);
        Assert.DoesNotContain("for (int i = 0; i < LstCodingEvents.Items.Count; i++)", listItems);
        Assert.Contains("\"ZoneDot\"", listItems);
        Assert.Contains("\"TxtConfidence\"", listItems);
        Assert.Contains("RefreshHighlights: ApplyCodingProtocolMatchListHighlights", listItems);
        Assert.Contains("actions.TryApplyItem(i)", workflow);
        Assert.Contains("actions.RefreshHighlights()", workflow);
    }

    [Fact]
    public void PlayerWindow_coding_side_panel_width_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingSidePanelWidthPolicy.cs");

        Assert.True(File.Exists(policyPath), "Breitenentscheidung fuer das Coding-Detailpanel muss ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingSidePanelWidthPolicy.Resolve", detail);
        Assert.DoesNotContain("Math.Clamp(availableWidth * 0.46", detail);
        Assert.DoesNotContain("return 760", detail);
        Assert.Contains("public static double Resolve", policy);
        Assert.Contains("WidthRatio = 0.46", policy);
    }

    [Fact]
    public void PlayerWindow_inline_defect_actions_live_in_actions_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs");
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var editApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventEditApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectDecisionWorkflow.cs");
        var acceptCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectAcceptCommandWorkflow.cs");
        var editCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectEditCommandWorkflow.cs");
        var rejectCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectRejectCommandWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Inline-Defekt-Aktionshandler sollen aus dem allgemeinen EventDetails-Partial heraus.");
        Assert.True(File.Exists(deleteApplierPath), "Inline-Defekt-Ablehnen muss die gemeinsame Coding-Event-Loeschanwendung nutzen.");
        Assert.True(File.Exists(editApplierPath), "Inline-Defekt-Bearbeiten muss die gemeinsame Coding-Event-Edit-Anwendung nutzen.");
        Assert.True(File.Exists(workflowPath), "Inline-Defekt-Entscheidungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(acceptCommandWorkflowPath), "Inline-Defekt-Accept-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editCommandWorkflowPath), "Inline-Defekt-Edit-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(rejectCommandWorkflowPath), "Inline-Defekt-Reject-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var detail = File.ReadAllText(detailPath);
        var actions = File.ReadAllText(actionsPath);
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var editApplier = File.ReadAllText(editApplierPath);
        var workflow = File.ReadAllText(workflowPath);
        var acceptCommandWorkflow = File.Exists(acceptCommandWorkflowPath) ? File.ReadAllText(acceptCommandWorkflowPath) : "";
        var editCommandWorkflow = File.Exists(editCommandWorkflowPath) ? File.ReadAllText(editCommandWorkflowPath) : "";
        var rejectCommandWorkflow = File.Exists(rejectCommandWorkflowPath) ? File.ReadAllText(rejectCommandWorkflowPath) : "";

        Assert.DoesNotContain("private void CodingAcceptDefect_Click", detail);
        Assert.DoesNotContain("private void CodingEditDefect_Click", detail);
        Assert.DoesNotContain("private void CodingRejectDefect_Click", detail);
        Assert.Contains("private void CodingAcceptDefect_Click", actions);
        Assert.Contains("private void CodingEditDefect_Click", actions);
        Assert.Contains("private void CodingRejectDefect_Click", actions);
        Assert.Contains("CodingInlineDefectAcceptCommandWorkflow.Execute", actions);
        Assert.Contains("CodingInlineDefectEditCommandWorkflow.Execute", actions);
        Assert.Contains("CodingInlineDefectRejectCommandWorkflow.Execute", actions);
        Assert.Contains("CodingInlineDefectDecisionWorkflow.CompleteEdit", actions);
        Assert.Contains("_codingSessionHost", actions);
        Assert.DoesNotContain("_codingVm", actions);
        Assert.DoesNotContain("CodingEventEditApplier.Apply", actions);
        Assert.DoesNotContain("CodingEventDeleteApplier.Apply", actions);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", actions);
        Assert.DoesNotContain("ev.MeterAtCapture = entry.MeterStart", actions);
        Assert.DoesNotContain("_codingSessionService?.RemoveEvent", actions);
        Assert.DoesNotContain("_codingVm.Events.Remove", actions);
        Assert.Contains("actions.AcceptDefect()", acceptCommandWorkflow);
        Assert.Contains("actions.UpdateInlineDefectDetail(acceptedDefect)", acceptCommandWorkflow);
        Assert.Contains("actions.FadeOutAiOverlayAfterAction()", acceptCommandWorkflow);
        Assert.Contains("actions.SelectDefect(selected)", editCommandWorkflow);
        Assert.Contains("actions.PausePlayback()", editCommandWorkflow);
        Assert.Contains("actions.TryEdit(selected)", editCommandWorkflow);
        Assert.Contains("actions.CompleteEdit(selected)", editCommandWorkflow);
        Assert.Contains("actions.RefreshEvents()", editCommandWorkflow);
        Assert.Contains("actions.UpdateInlineDefectDetail(selected)", editCommandWorkflow);
        Assert.Contains("actions.RejectDefect()", rejectCommandWorkflow);
        Assert.Contains("actions.HideInlineDefectDetail()", rejectCommandWorkflow);
        Assert.Contains("actions.FadeOutAiOverlayAfterAction()", rejectCommandWorkflow);
        Assert.Contains("CodingEventEditApplier.Apply", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("codingSessionService?.UpdateEvent", editApplier);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }
}
