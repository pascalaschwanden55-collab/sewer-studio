using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

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
        AssertNoForbiddenTokens(
            detail,
            "if (selection.SelectedEvent is not null)",
            "LstCodingEvents.SelectedItem is CodingEvent",
            "TxtInlineDetailCode.Text = state.CodeText",
            "BtnInlineAccept.Visibility = state.CanAct",
            "ImgInlineEvidencePreview.Source = null",
            "$\"{ev.MeterAtCapture:F2}m\"",
            "$\"{conf * 100:F0}%\"");
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
        AssertNoForbiddenTokens(
            detail,
            "private void UpdateInlineEvidencePreview",
            "CodingDefectPreviewService.BuildPreviewImagePath",
            "BitmapImage");
        Assert.Contains("private void UpdateInlineEvidencePreview", preview);
        Assert.Contains("CodingInlineEvidencePreviewWorkflow.Execute", preview);
        Assert.Contains("_protocolContext.CodingDefectPreviews", preview);
        AssertNoForbiddenTokens(
            preview,
            "CodingInlineEvidencePreviewService.Build",
            "catch (Exception");
        Assert.Contains("CodingInlineEvidencePreviewService.Build", previewWorkflow);
        Assert.Contains("CodingInlineEvidencePreviewService.LoadFailed", previewWorkflow);
        Assert.Contains("_codingSidePanelControllers.InlineDefectDetail.ApplyPreview", preview);
        AssertNoForbiddenTokens(
            preview,
            "ImgInlineEvidencePreview.Source = state.Source",
            "ImgInlineEvidencePreview.Visibility = state.ImageVisible",
            "TxtInlineEvidencePreviewStatus.Text = state.StatusText",
            "TxtInlineEvidencePreviewStatus.Visibility = state.StatusVisible");
        Assert.Contains("public void ApplyPreview", File.ReadAllText(Path.Combine(uiRoot, "Ai", "CodingInlineDefectDetailControls.cs")));
        AssertNoForbiddenTokens(
            preview,
            "CodingDefectPreviewService.BuildPreviewImagePath",
            "BitmapImage");
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
        AssertNoForbiddenTokens(
            detail,
            "while (dep != null && dep is not ListBoxItem)",
            "VisualTreeHelper.GetParent(dep)");
        Assert.Contains("public static bool SelectContainingListBoxItem", helper);
        Assert.Contains("VisualTreeSafe.FindAncestor<ListBoxItem>", helper);
        AssertNoForbiddenTokens(
            helper,
            "VisualTreeHelper.GetParent",
            "LogicalTreeHelper.GetParent");
    }

    [Fact]
    public void PlayerWindow_coding_event_list_item_coloring_lives_in_visual_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var oldListItemsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.ListItems.cs");
        var visualControllerPath = Path.Combine(uiRoot, "Player", "CodingEventListVisualController.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventListItemColorizeWorkflow.cs");

        Assert.True(File.Exists(visualControllerPath), "Event-ListBox-Einfaerbung soll in einem eigenen Controller liegen.");
        Assert.True(File.Exists(workflowPath), "Event-ListBox-Einfaerbungsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.False(File.Exists(oldListItemsPath), "Das alte ListItems-Partial muss entfernt bleiben.");

        var detail = File.ReadAllText(detailPath);
        var visualController = File.ReadAllText(visualControllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        AssertNoForbiddenTokens(
            detail,
            "private void ColorizeCodingEventListItems",
            "\"ZoneDot\"",
            "\"TxtConfidence\"");
        Assert.Contains("public void ColorizeCodingEvents", visualController);
        Assert.Contains("CodingEventListItemColorizeWorkflow.Execute", visualController);
        AssertNoForbiddenTokens(visualController, "for (int i = 0; i < LstCodingEvents.Items.Count; i++)");
        Assert.Contains("\"ZoneDot\"", visualController);
        Assert.Contains("\"TxtConfidence\"", visualController);
        Assert.Contains("\"TxtStatusIcon\"", visualController);
        Assert.Contains("RefreshHighlights: ApplyProtocolMatchHighlights", visualController);
        Assert.Contains(
            "ColorizeListItems: _codingEventListVisualController.ColorizeCodingEvents",
            windowRoot);
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
        AssertNoForbiddenTokens(
            detail,
            "Math.Clamp(availableWidth * 0.46",
            "return 760");
        Assert.Contains("public static double Resolve", policy);
        Assert.Contains("WidthRatio = 0.46", policy);
    }

    [Fact]
    public void PlayerWindow_inline_defect_actions_use_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var accessorsPath = Path.Combine(windowsRoot, "PlayerWindow.CodingSidePanelAccessors.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingInlineDefectController.cs");
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var editApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventEditApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectDecisionWorkflow.cs");
        var acceptCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectAcceptCommandWorkflow.cs");
        var editCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectEditCommandWorkflow.cs");
        var rejectCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectRejectCommandWorkflow.cs");

        Assert.False(File.Exists(actionsPath), "Inline-Defekt-Aktionen dürfen nicht wieder als PlayerWindow-Partial erscheinen.");
        Assert.True(File.Exists(controllerPath), "Inline-Defekt-Aktionen brauchen einen eigenen Controller.");
        Assert.True(File.Exists(deleteApplierPath), "Inline-Defekt-Ablehnen muss die gemeinsame Coding-Event-Loeschanwendung nutzen.");
        Assert.True(File.Exists(editApplierPath), "Inline-Defekt-Bearbeiten muss die gemeinsame Coding-Event-Edit-Anwendung nutzen.");
        Assert.True(File.Exists(workflowPath), "Inline-Defekt-Entscheidungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(acceptCommandWorkflowPath), "Inline-Defekt-Accept-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editCommandWorkflowPath), "Inline-Defekt-Edit-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(rejectCommandWorkflowPath), "Inline-Defekt-Reject-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var detail = File.ReadAllText(detailPath);
        var controller = File.ReadAllText(controllerPath);
        var state = File.ReadAllText(statePath);
        var accessors = File.ReadAllText(accessorsPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var editApplier = File.ReadAllText(editApplierPath);
        var workflow = File.ReadAllText(workflowPath);
        var acceptCommandWorkflow = File.Exists(acceptCommandWorkflowPath) ? File.ReadAllText(acceptCommandWorkflowPath) : "";
        var editCommandWorkflow = File.Exists(editCommandWorkflowPath) ? File.ReadAllText(editCommandWorkflowPath) : "";
        var rejectCommandWorkflow = File.Exists(rejectCommandWorkflowPath) ? File.ReadAllText(rejectCommandWorkflowPath) : "";

        AssertNoForbiddenTokens(
            detail,
            "private void CodingAcceptDefect_Click",
            "private void CodingEditDefect_Click",
            "private void CodingRejectDefect_Click");
        Assert.Contains("public interface ICodingInlineDefectController", controller);
        Assert.Contains("public sealed class CodingInlineDefectController", controller);
        Assert.Contains("CodingInlineDefectAcceptCommandWorkflow.Execute", controller);
        Assert.Contains("CodingInlineDefectEditCommandWorkflow.Execute", controller);
        Assert.Contains("CodingInlineDefectRejectCommandWorkflow.Execute", controller);
        Assert.Contains("CodingInlineDefectDecisionWorkflow.CompleteEdit", controller);
        Assert.Contains("private readonly ICodingInlineDefectController _codingInlineDefectController", state);
        Assert.Contains("_codingInlineDefectController.Accept()", accessors);
        Assert.Contains("_codingInlineDefectController.Edit()", accessors);
        Assert.Contains("_codingInlineDefectController.Reject()", accessors);
        Assert.Contains("_codingSessionHost", windowRoot);
        AssertNoForbiddenTokens(
            controller,
            "CodingEventEditApplier.Apply",
            "CodingEventDeleteApplier.Apply",
            "_codingSessionService?.UpdateEvent",
            "ev.MeterAtCapture = entry.MeterStart",
            "_codingSessionService?.RemoveEvent");
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

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene alte Inline-Defekt-Logik gefunden: " + string.Join(", ", hits));
    }
}
