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
    PlayerPositionInputController PositionInputController,
    PlayerPositionSliderStateController PositionSliderStateController,
    PlayerKeyboardActionControllerOwner KeyboardActionControllerOwner,
    PlayerShortcutOverlayController ShortcutOverlayController,
    PlayerControlInputController ControlInputController,
    PlayerWindowShutdownStateController ShutdownStateController,
    PlayerWindowTimerController TimerController,
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
    FrameworkElement ShortcutOverlay,
    Slider VolumeSlider,
    TextBlock VolumeText,
    ToggleButton MuteButton,
    TextBlock MuteIcon,
    Slider OverlayOpacitySlider,
    TextBlock OverlayOpacityText,
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
    IPlayerControlSettingsStore PlayerSettings,
    string VideoPath,
    Action EnsurePlaying,
    Action UpdateUi,
    Action<float> ShowUnsupportedRate,
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
        var positionControls = new PlayerPositionControls(
            controls.PositionSlider,
            controls.CurrentTimeText,
            controls.DurationText);
        var positionInputController = new PlayerPositionInputController(
            controls.PositionSlider,
            dependencies.TimelineHost,
            positionControls,
            dependencies.UpdateUi);
        var speedControls = new PlayerSpeedControls(
            controls.RateText,
            controls.SpeedSlider,
            controls.Speed05Button,
            controls.Speed1Button,
            controls.Speed15Button,
            controls.Speed2Button,
            controls.Speed4Button,
            controls.Speed8Button);
        var settingsView = new PlayerControlSettingsView(
            controls.VolumeSlider,
            controls.VolumeText,
            controls.MuteButton,
            controls.MuteIcon,
            controls.OverlayOpacitySlider,
            controls.OverlayOpacityText,
            controls.CodingOverlayCanvas,
            controls.DetectionCanvas,
            dependencies.PlaybackControlHost.SetVolume,
            dependencies.PlaybackControlHost.SetMute);
        var controlInputController = new PlayerControlInputController(
            new PlayerControlSettingsController(dependencies.PlayerSettings),
            settingsView,
            dependencies.PlaybackControlHost,
            speedControls,
            dependencies.ShowUnsupportedRate);
        var timerController = PlayerWindowTimerController.Create(
            createRequest: () => new PlayerWindowTimerTickWorkflowRequest(
                shutdownStateController.IsClosing,
                shutdownStateController.IsPlaybackDisposed,
                positionSliderStateController.IsDragging),
            actions: new PlayerWindowTimerTickWorkflowActions(
                dependencies.UpdateUi,
                () => positionInputController.ScrubSeekToSlider()));

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
            positionControls,
            positionInputController,
            positionSliderStateController,
            keyboardActionControllerOwner,
            new PlayerShortcutOverlayController(controls.ShortcutOverlay),
            controlInputController,
            shutdownStateController,
            timerController,
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
