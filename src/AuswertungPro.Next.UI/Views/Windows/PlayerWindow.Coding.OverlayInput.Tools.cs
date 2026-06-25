using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private string? _activeCodingToolName;

    private void CodingToolRect_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Markieren");

    private void SetCodingTool(
        object activeBtn,
        OverlayToolType tool,
        SchemaType? schemaType = null,
        LevelMode? levelMode = null)
    {
        var btnName = (activeBtn as FrameworkElement)?.Name ?? "";
        var label = (activeBtn as ContentControl)?.Content?.ToString() ?? tool.ToString();

        CodingToolSelectionWorkflow.Execute(
            new CodingToolSelectionWorkflowRequest(
                _codingOverlayToolHost.HasOverlayService,
                _codingSessionHost.HasViewModel,
                _activeCodingToolName,
                btnName,
                label,
                tool,
                schemaType,
                levelMode),
            new CodingToolSelectionWorkflowActions(
                ResetCalibration: () =>
                {
                    _codingIsCalibrating = false;
                    _codingCalibStart = null;
                },
                CloseToolsDropdown: () => { ToolsDropdownPopup.IsOpen = false; },
                SetActiveToolName: activeToolName => _activeCodingToolName = activeToolName,
                SetActiveLevelMode: mode => { _codingOverlayToolHost.SetActiveLevelMode(mode); },
                SetActiveTool: activeTool => { _codingOverlayToolHost.SetActiveTool(activeTool); },
                SetActiveSchemaType: activeSchemaType => _codingSchemaType = activeSchemaType,
                CancelSchema: _codingSchemaManager.Cancel,
                ApplyActiveToolSelection: labelText => CodingOverlayInputControls.ApplyActiveToolSelection(
                    TxtActiveToolLabel,
                    BtnCodingCreateEvent,
                    labelText),
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                ClearOverlayInfo: () => UpdateCodingOverlayInfo(null),
                UpdateOverlayCursor: UpdateCodingOverlayCursor,
                RedrawCodingCanvas: includeManualOverlay => RedrawCodingCanvas(includeManualOverlay)));
    }

    private void CodingScreenshot_Click(object sender, RoutedEventArgs e)
        => CodingScreenshotCommandWorkflow.Execute(
            new CodingScreenshotCommandActions(
                CopyWindowToClipboard: () => WindowClipboardCaptureService.TryCopyWindowToClipboard(this),
                ShowToast: ShowCodingScreenshotToast));

    private void ShowCodingScreenshotToast(string msg)
    {
        try
        {
            LiveDetectionStatusControls.ShowStatusMessage(LiveDetectionStatusText, msg);
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            t.Tick += (s, ev) =>
            {
                LiveDetectionStatusControls.HideDetectionStatus(LiveDetectionStatusText);
                t.Stop();
            };
            t.Start();
        }
        catch { }
    }

    private void UpdateCodingOverlayCursor()
    {
        var activeTool = _codingOverlayToolHost.ActiveTool;
        CodingOverlayInputControls.ApplyCanvasCursor(
            CodingOverlayCanvas,
            CodingOverlayCursorPolicy.ShouldUseCrossCursor(
                CodingOverlayPopup.IsOpen,
                _codingIsCalibrating,
                activeTool));
    }
}
