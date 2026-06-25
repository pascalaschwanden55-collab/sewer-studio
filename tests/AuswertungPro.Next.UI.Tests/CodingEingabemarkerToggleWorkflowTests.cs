using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerToggleWorkflowTests
{
    [Fact]
    public void Execute_activates_marker_when_button_is_checked()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerToggleWorkflow.Execute(
            new CodingEingabemarkerToggleWorkflowRequest(IsChecked: true),
            Actions(calls));

        Assert.Equal(CodingEingabemarkerToggleWorkflowOutcome.Activated, result.Outcome);
        Assert.Equal(
            [
                "pause",
                "phase:drawing",
                "ensure-overlay",
                "open-popup",
                "update-viewport",
                "enable-canvas",
                "status"
            ],
            calls);
    }

    [Fact]
    public void Execute_cancels_marker_when_button_is_unchecked()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerToggleWorkflow.Execute(
            new CodingEingabemarkerToggleWorkflowRequest(IsChecked: false),
            Actions(calls));

        Assert.Equal(CodingEingabemarkerToggleWorkflowOutcome.Cancelled, result.Outcome);
        Assert.Equal(
            [
                "phase:inactive",
                "uncheck",
                "hide-popup",
                "clear-preview",
                "reset-cursor"
            ],
            calls);
    }

    private static CodingEingabemarkerToggleWorkflowActions Actions(List<string> calls)
        => new(
            PauseForCodingInteraction: () => calls.Add("pause"),
            SetDrawingPhase: () => calls.Add("phase:drawing"),
            EnsureMarkOverlayReady: () => calls.Add("ensure-overlay"),
            OpenCodingOverlayPopup: () => calls.Add("open-popup"),
            UpdateCodingOverlayViewport: () => calls.Add("update-viewport"),
            EnableDrawingCanvas: () => calls.Add("enable-canvas"),
            ShowDrawingStatus: () => calls.Add("status"),
            SetInactivePhase: () => calls.Add("phase:inactive"),
            UncheckButton: () => calls.Add("uncheck"),
            HideInputPopup: () => calls.Add("hide-popup"),
            ClearPreview: () => calls.Add("clear-preview"),
            ResetCanvasCursor: () => calls.Add("reset-cursor"));
}
