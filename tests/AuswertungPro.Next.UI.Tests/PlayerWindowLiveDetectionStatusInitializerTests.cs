using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionStatusInitializerTests
{
    [Fact]
    public void Create_wires_all_status_controls_and_shared_pulse_state()
    {
        RunOnStaThread(() =>
        {
            var pulseRing = new Border { Opacity = 0 };
            var badge = new Border { Visibility = Visibility.Collapsed };
            var badgeStatus = new TextBlock();
            var badgeDot = new Ellipse();
            var yoloBar = new Border { Visibility = Visibility.Collapsed };
            var yoloStatus = new TextBlock();
            var yoloDot = new Ellipse();
            var yoloModel = new TextBlock();
            var codingStatus = new TextBlock();
            var codingStage = new TextBlock();
            var codingDot = new Ellipse();
            var detectionStatus = new TextBlock();
            var findingPanel = new Border { Visibility = Visibility.Collapsed };
            var findingSummary = new TextBlock();
            var pulseState = new LiveDetectionPulseStateController();
            var controllers = PlayerWindowLiveDetectionStatusInitializer.Create(
                new PlayerWindowLiveDetectionStatusControls(
                    pulseRing,
                    badge,
                    badgeStatus,
                    badgeDot,
                    yoloBar,
                    yoloStatus,
                    yoloDot,
                    yoloModel,
                    codingStatus,
                    codingStage,
                    codingDot,
                    detectionStatus,
                    findingPanel,
                    findingSummary),
                pulseState,
                Dispatcher.CurrentDispatcher);

            controllers.Status.SetLiveDetectionBadge(
                "KI aktiv",
                PlayerStatusColors.Success,
                "YOLO");
            controllers.Status.SetYoloStatus(
                "Bereit",
                PlayerStatusColors.Warning,
                "yolo26m");
            controllers.Status.SetCodingAiState(
                "Analyse",
                PlayerStatusColors.Success,
                "DINO",
                pulse: true);
            var detection = new LiveDetection(
                4.25,
                [new LiveFrameFinding("Riss", 3, "3", 20)],
                null,
                null);
            controllers.Status.UpdateDetectionStatus(detection);

            Assert.IsType<LiveDetectionPulseController>(controllers.Pulse);
            Assert.IsType<LiveDetectionStatusController>(controllers.Status);
            Assert.Equal(Visibility.Visible, badge.Visibility);
            Assert.Equal("KI aktiv | YOLO", badgeStatus.Text);
            Assert.Equal(
                PlayerStatusColors.Success,
                Assert.IsType<SolidColorBrush>(badgeDot.Fill).Color);
            Assert.Equal(Visibility.Visible, yoloBar.Visibility);
            Assert.Equal("YOLO: Bereit", yoloStatus.Text);
            Assert.Equal("yolo26m", yoloModel.Text);
            Assert.Equal(
                PlayerStatusColors.Warning,
                Assert.IsType<SolidColorBrush>(yoloDot.Fill).Color);
            Assert.Equal("Analyse", codingStatus.Text);
            Assert.Equal("DINO", codingStage.Text);
            Assert.Equal(
                PlayerStatusColors.Success,
                Assert.IsType<SolidColorBrush>(codingDot.Fill).Color);
            Assert.True(pulseState.IsRunning);
            Assert.True(pulseRing.HasAnimatedProperties);
            var pulseScale = Assert.IsType<ScaleTransform>(pulseRing.RenderTransform);
            Assert.True(pulseScale.HasAnimatedProperties);
            Assert.Equal(
                LiveDetectionDisplayPolicy.BuildDetectionStatusText(detection),
                detectionStatus.Text);
            Assert.Equal(Visibility.Visible, findingPanel.Visibility);
            Assert.Contains("Riss", findingSummary.Text, StringComparison.Ordinal);

            controllers.Pulse.Stop();

            Assert.False(pulseState.IsRunning);
            Assert.False(pulseRing.HasAnimatedProperties);
            Assert.False(pulseScale.HasAnimatedProperties);
            Assert.Equal(0, pulseRing.Opacity);
            Assert.Equal(1, pulseScale.ScaleX);
            Assert.Equal(1, pulseScale.ScaleY);

            controllers.Status.SetCodingAiState(
                "Analyse",
                PlayerStatusColors.Success,
                "DINO",
                pulse: true);

            Assert.True(pulseState.IsRunning);
            Assert.True(pulseRing.HasAnimatedProperties);

            controllers.Status.SetCodingAiState(
                "Bereit",
                PlayerStatusColors.Muted,
                stage: null,
                pulse: false);

            Assert.False(pulseState.IsRunning);
            Assert.False(pulseRing.HasAnimatedProperties);
            Assert.False(pulseScale.HasAnimatedProperties);
            Assert.Equal(0, pulseRing.Opacity);
            Assert.Equal(1, pulseScale.ScaleX);
            Assert.Equal(1, pulseScale.ScaleY);
        });
    }

    [Fact]
    public void Create_rejects_missing_top_level_dependencies()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateControls();
            var pulseState = new LiveDetectionPulseStateController();
            var dispatcher = Dispatcher.CurrentDispatcher;

            Assert.Throws<ArgumentNullException>(() =>
                PlayerWindowLiveDetectionStatusInitializer.Create(null!, pulseState, dispatcher));
            Assert.Throws<ArgumentNullException>(() =>
                PlayerWindowLiveDetectionStatusInitializer.Create(controls, null!, dispatcher));
            Assert.Throws<ArgumentNullException>(() =>
                PlayerWindowLiveDetectionStatusInitializer.Create(controls, pulseState, null!));
        });
    }

    private static PlayerWindowLiveDetectionStatusControls CreateControls()
        => new(
            new Border(),
            new Border(),
            new TextBlock(),
            new Ellipse(),
            new Border(),
            new TextBlock(),
            new Ellipse(),
            new TextBlock(),
            new TextBlock(),
            new TextBlock(),
            new Ellipse(),
            new TextBlock(),
            new Border(),
            new TextBlock());

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
