using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerWindowControllerSet(
    DamageMarkerController DamageMarkerController,
    QuickScanController QuickScanController,
    PlayerPositionControls PositionControls,
    PlayerPositionSliderStateController PositionSliderStateController,
    PlayerKeyboardActionControllerOwner KeyboardActionControllerOwner,
    PlayerWindowShutdownStateController ShutdownStateController,
    PlayerWindowTimerController TimerController,
    PlayerSpeedControls SpeedControls,
    PlayerMarkToolControls MarkToolControls,
    CodingOverlayRenderController CodingOverlayRenderController,
    LiveDetectionController LiveDetectionController);

public sealed record PlayerWindowControllerSetControls(
    Canvas DamageMarkerCanvas,
    Slider PositionSlider,
    Canvas HeatmapCanvas,
    ToggleButton QuickScanButton,
    TextBlock QuickScanStatusText,
    TextBlock CurrentTimeText,
    TextBlock DurationText,
    TextBlock RateText,
    Slider SpeedSlider,
    ToggleButton Speed05Button,
    ToggleButton Speed1Button,
    ToggleButton Speed15Button,
    ToggleButton Speed2Button,
    ToggleButton Speed4Button,
    ToggleButton Speed8Button,
    Popup MarkToolPopup,
    Popup CodingMarkToolPopup,
    Popup ToolsDropdownPopup,
    TextBlock MarkToolName,
    TextBlock ActiveToolLabel,
    UIElement DetectionOverlayGrid,
    Canvas DetectionCanvas,
    Popup CodingOverlayPopup,
    Canvas CodingOverlayCanvas);

public sealed record PlayerWindowControllerSetDependencies(
    PlayerDamageOverlayData? DamageOverlay,
    PlayerPlaybackControlHost PlaybackControlHost,
    PlayerTimelineHost TimelineHost,
    string VideoPath,
    Action EnsurePlaying,
    Action UpdateUi,
    Action ScrubSeekToSlider,
    Func<(double offsetX, double trackWidth)> ResolveSliderTrackBounds,
    Func<NormalizedPoint, Point> MapCodingOverlayPoint);

public static class PlayerWindowControllerSetFactory
{
    public static PlayerWindowControllerSet Create(
        PlayerWindowControllerSetControls controls,
        PlayerWindowControllerSetDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(dependencies);

        var positionSliderStateController = new PlayerPositionSliderStateController();
        var keyboardActionControllerOwner = new PlayerKeyboardActionControllerOwner();
        var shutdownStateController = new PlayerWindowShutdownStateController();
        var timerController = PlayerWindowTimerController.Create(
            createRequest: () => new PlayerWindowTimerTickWorkflowRequest(
                shutdownStateController.IsClosing,
                shutdownStateController.IsPlaybackDisposed,
                positionSliderStateController.IsDragging),
            actions: new PlayerWindowTimerTickWorkflowActions(
                dependencies.UpdateUi,
                dependencies.ScrubSeekToSlider));

        return new PlayerWindowControllerSet(
            new DamageMarkerController(
                controls.DamageMarkerCanvas,
                controls.PositionSlider,
                dependencies.DamageOverlay,
                dependencies.PlaybackControlHost,
                dependencies.TimelineHost,
                dependencies.EnsurePlaying,
                dependencies.UpdateUi,
                dependencies.ResolveSliderTrackBounds),
            new QuickScanController(
                controls.HeatmapCanvas,
                controls.QuickScanButton,
                controls.QuickScanStatusText,
                dependencies.PlaybackControlHost,
                dependencies.TimelineHost,
                dependencies.VideoPath,
                dependencies.EnsurePlaying,
                dependencies.UpdateUi,
                dependencies.ResolveSliderTrackBounds),
            new PlayerPositionControls(
                controls.PositionSlider,
                controls.CurrentTimeText,
                controls.DurationText),
            positionSliderStateController,
            keyboardActionControllerOwner,
            shutdownStateController,
            timerController,
            new PlayerSpeedControls(
                controls.RateText,
                controls.SpeedSlider,
                controls.Speed05Button,
                controls.Speed1Button,
                controls.Speed15Button,
                controls.Speed2Button,
                controls.Speed4Button,
                controls.Speed8Button),
            new PlayerMarkToolControls(
                controls.MarkToolPopup,
                controls.CodingMarkToolPopup,
                controls.ToolsDropdownPopup,
                controls.MarkToolName,
                controls.ActiveToolLabel,
                controls.DetectionOverlayGrid,
                controls.DetectionCanvas,
                controls.CodingOverlayPopup,
                controls.CodingOverlayCanvas),
            new CodingOverlayRenderController(
                new CanvasOverlaySurface(controls.CodingOverlayCanvas),
                new DelegateOverlayCoordinateMapper(dependencies.MapCodingOverlayPoint)),
            new LiveDetectionController());
    }
}
