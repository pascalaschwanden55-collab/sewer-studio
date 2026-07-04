using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowControllerSetFactoryTests
{
    [Fact]
    public void Create_builds_player_window_controller_bundle()
    {
        RunOnStaThread(() =>
        {
            var controls = new PlayerWindowControllerSetControls(
                DamageMarkerCanvas: new Canvas(),
                PositionSlider: new Slider(),
                HeatmapCanvas: new Canvas(),
                QuickScanButton: new ToggleButton(),
                QuickScanStatusText: new TextBlock(),
                CurrentTimeText: new TextBlock(),
                DurationText: new TextBlock(),
                RateText: new TextBlock(),
                SpeedSlider: new Slider(),
                Speed05Button: new ToggleButton(),
                Speed1Button: new ToggleButton(),
                Speed15Button: new ToggleButton(),
                Speed2Button: new ToggleButton(),
                Speed4Button: new ToggleButton(),
                Speed8Button: new ToggleButton(),
                MarkToolPopup: new Popup(),
                CodingMarkToolPopup: new Popup(),
                ToolsDropdownPopup: new Popup(),
                MarkToolName: new TextBlock(),
                ActiveToolLabel: new TextBlock(),
                DetectionOverlayGrid: new Grid(),
                DetectionCanvas: new Canvas(),
                CodingOverlayPopup: new Popup(),
                CodingOverlayCanvas: new Canvas());
            var playbackHost = new PlayerPlaybackControlHost(
                readIsPlaying: () => false,
                setPause: _ => { },
                play: () => { },
                stop: () => { },
                readRate: () => 1.0f,
                setRate: _ => 0,
                readVolume: () => 80,
                setVolume: _ => { },
                readMute: () => false,
                setMute: _ => { },
                shouldStartPlayback: () => false,
                playPath: _ => { });
            var timelineHost = new PlayerTimelineHost(
                readTimeMilliseconds: () => 0,
                readLengthMilliseconds: () => 1000,
                seekMilliseconds: _ => { },
                setPositionRatio: _ => { });
            var dependencies = new PlayerWindowControllerSetDependencies(
                DamageOverlay: null,
                PlaybackControlHost: playbackHost,
                TimelineHost: timelineHost,
                VideoPath: "sample.mp4",
                EnsurePlaying: () => { },
                UpdateUi: () => { },
                ScrubSeekToSlider: () => { },
                ResolveSliderTrackBounds: () => (0, 100),
                MapCodingOverlayPoint: _ => new Point(1, 2));

            var set = PlayerWindowControllerSetFactory.Create(controls, dependencies);

            Assert.NotNull(set.DamageMarkerController);
            Assert.NotNull(set.QuickScanController);
            Assert.NotNull(set.PositionControls);
            Assert.NotNull(set.SpeedControls);
            Assert.NotNull(set.MarkToolControls);
            Assert.NotNull(set.CodingOverlayRenderController);
            Assert.Contains(
                typeof(PlayerWindowControllerSet).GetProperties(),
                property => property.Name == "TimerController"
                    && property.PropertyType == typeof(PlayerWindowTimerController));
            Assert.Contains(
                typeof(PlayerWindowControllerSet).GetProperties(),
                property => property.Name == "ShutdownStateController"
                    && property.PropertyType == typeof(PlayerWindowShutdownStateController));
            Assert.Contains(
                typeof(PlayerWindowControllerSet).GetProperties(),
                property => property.Name == "KeyboardActionControllerOwner"
                    && property.PropertyType == typeof(PlayerKeyboardActionControllerOwner));
            Assert.Contains(
                typeof(PlayerWindowControllerSet).GetProperties(),
                property => property.Name == "PositionSliderStateController"
                    && property.PropertyType == typeof(PlayerPositionSliderStateController));
            Assert.Contains(
                typeof(PlayerWindowControllerSet).GetProperties(),
                property => property.Name == "LiveDetectionController"
                    && property.PropertyType == typeof(LiveDetectionController));
        });
    }

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
