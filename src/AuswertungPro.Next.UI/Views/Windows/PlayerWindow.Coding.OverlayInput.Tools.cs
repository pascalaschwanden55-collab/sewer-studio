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
        if (_codingOverlayService == null || _codingVm == null) return;
        _codingIsCalibrating = false;
        _codingCalibStart = null;

        ToolsDropdownPopup.IsOpen = false;

        var btnName = (activeBtn as FrameworkElement)?.Name ?? "";
        var label = (activeBtn as ContentControl)?.Content?.ToString() ?? tool.ToString();
        var selection = CodingToolSelectionPolicy.Build(
            _activeCodingToolName,
            btnName,
            label,
            tool,
            schemaType,
            levelMode);

        _activeCodingToolName = selection.ActiveToolName;
        if (selection.LevelModeToApply.HasValue)
            _codingOverlayService.ActiveLevelMode = selection.LevelModeToApply.Value;

        _codingOverlayService.ActiveTool = selection.ActiveTool;
        _codingSchemaType = selection.ActiveSchemaType;
        _codingSchemaManager.Cancel();

        TxtActiveToolLabel.Text = selection.LabelText;

        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private void CodingScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (WindowClipboardCaptureService.TryCopyWindowToClipboard(this))
            ShowCodingScreenshotToast("Fenster in Zwischenablage kopiert");
    }

    private void ShowCodingScreenshotToast(string msg)
    {
        try
        {
            LiveDetectionStatusText.Text = msg;
            LiveDetectionStatusText.Visibility = Visibility.Visible;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            t.Tick += (s, ev) => { LiveDetectionStatusText.Visibility = Visibility.Collapsed; t.Stop(); };
            t.Start();
        }
        catch { }
    }

    private void UpdateCodingOverlayCursor()
    {
        var activeTool = _codingOverlayService?.ActiveTool ?? OverlayToolType.None;
        CodingOverlayCanvas.Cursor = CodingOverlayCursorPolicy.ShouldUseCrossCursor(
            CodingOverlayPopup.IsOpen,
            _codingIsCalibrating,
            activeTool)
            ? Cursors.Cross
            : Cursors.Arrow;
    }
}
