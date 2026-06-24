using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerMarkToolControlsTests
{
    [Fact]
    public void ToggleManualMarkPopup_uses_live_popup_outside_coding_mode()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateControls(out var state);

            controls.ToggleManualMarkPopup(isCodingMode: false);

            Assert.True(state.MarkToolPopup.IsOpen);
            Assert.False(state.ToolsDropdownPopup.IsOpen);
        });
    }

    [Fact]
    public void ToggleManualMarkPopup_uses_tools_dropdown_in_coding_mode()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateControls(out var state);

            controls.ToggleManualMarkPopup(isCodingMode: true);

            Assert.False(state.MarkToolPopup.IsOpen);
            Assert.True(state.ToolsDropdownPopup.IsOpen);
        });
    }

    [Fact]
    public void BeginActivation_closes_popups_and_sets_tool_labels()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateControls(out var state);
            state.MarkToolPopup.IsOpen = true;
            state.CodingMarkToolPopup.IsOpen = true;
            state.ToolsDropdownPopup.IsOpen = true;

            controls.BeginActivation("Rechteck");

            Assert.False(state.MarkToolPopup.IsOpen);
            Assert.False(state.CodingMarkToolPopup.IsOpen);
            Assert.False(state.ToolsDropdownPopup.IsOpen);
            Assert.Equal("Rechteck", state.MarkToolName.Text);
            Assert.Equal("Rechteck", state.ActiveToolLabel.Text);
        });
    }

    [Fact]
    public void SetToolLabels_updates_labels_without_closing_popups()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateControls(out var state);
            state.MarkToolPopup.IsOpen = true;
            state.CodingMarkToolPopup.IsOpen = true;
            state.ToolsDropdownPopup.IsOpen = true;
            var setLabels = typeof(PlayerMarkToolControls).GetMethod(
                "SetToolLabels",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: [typeof(string)],
                modifiers: null);
            Assert.NotNull(setLabels);

            setLabels.Invoke(controls, ["Rechteck"]);

            Assert.True(state.MarkToolPopup.IsOpen);
            Assert.True(state.CodingMarkToolPopup.IsOpen);
            Assert.True(state.ToolsDropdownPopup.IsOpen);
            Assert.Equal("Rechteck", state.MarkToolName.Text);
            Assert.Equal("Rechteck", state.ActiveToolLabel.Text);
        });
    }

    [Fact]
    public void ActivatePointTool_enables_detection_overlay()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateControls(out var state);

            controls.ActivatePointTool();

            Assert.Equal(Visibility.Visible, state.DetectionOverlayGrid.Visibility);
            Assert.True(state.DetectionOverlayGrid.IsHitTestVisible);
            Assert.True(state.DetectionCanvas.IsHitTestVisible);
            Assert.Equal(Cursors.Cross, state.DetectionCanvas.Cursor);
        });
    }

    [Fact]
    public void Deactivate_resets_detection_and_collapses_when_detection_is_off()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateControls(out var state);
            controls.ActivatePointTool();

            controls.DeactivateDetectionSide(isDetecting: false);

            Assert.Equal(Cursors.Arrow, state.DetectionCanvas.Cursor);
            Assert.False(state.DetectionCanvas.IsHitTestVisible);
            Assert.False(state.DetectionOverlayGrid.IsHitTestVisible);
            Assert.Equal(Visibility.Collapsed, state.DetectionOverlayGrid.Visibility);
        });
    }

    [Fact]
    public void Coding_overlay_methods_open_and_close_drawing_surface()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateControls(out var state);

            controls.OpenCodingOverlay();
            controls.EnableCodingOverlayInput();

            Assert.True(state.CodingOverlayPopup.IsOpen);
            Assert.True(state.CodingOverlayCanvas.IsHitTestVisible);
            Assert.Equal(Cursors.Cross, state.CodingOverlayCanvas.Cursor);

            controls.DeactivateCodingOverlay();

            Assert.False(state.CodingOverlayPopup.IsOpen);
            Assert.False(state.CodingOverlayCanvas.IsHitTestVisible);
        });
    }

    private static PlayerMarkToolControls CreateControls(out MarkToolControlState state)
    {
        state = new MarkToolControlState(
            new Popup(),
            new Popup(),
            new Popup(),
            new TextBlock(),
            new TextBlock(),
            new Grid(),
            new Canvas(),
            new Popup(),
            new Canvas());

        return new PlayerMarkToolControls(
            state.MarkToolPopup,
            state.CodingMarkToolPopup,
            state.ToolsDropdownPopup,
            state.MarkToolName,
            state.ActiveToolLabel,
            state.DetectionOverlayGrid,
            state.DetectionCanvas,
            state.CodingOverlayPopup,
            state.CodingOverlayCanvas);
    }

    private sealed record MarkToolControlState(
        Popup MarkToolPopup,
        Popup CodingMarkToolPopup,
        Popup ToolsDropdownPopup,
        TextBlock MarkToolName,
        TextBlock ActiveToolLabel,
        Grid DetectionOverlayGrid,
        Canvas DetectionCanvas,
        Popup CodingOverlayPopup,
        Canvas CodingOverlayCanvas);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
