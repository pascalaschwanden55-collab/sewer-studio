using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionStatusControlsTests
{
    [Fact]
    public void ShowLiveDetectionBadge_sets_text_visibility_and_dot_color()
    {
        RunOnStaThread(() =>
        {
            var badge = new Border { Visibility = Visibility.Collapsed };
            var text = new TextBlock();
            var dot = new Ellipse();
            var show = FindMethod(
                "ShowLiveDetectionBadge",
                typeof(FrameworkElement),
                typeof(TextBlock),
                typeof(Shape),
                typeof(string),
                typeof(Color),
                typeof(string));
            Assert.NotNull(show);

            show.Invoke(null, [badge, text, dot, "KI aktiv", PlayerStatusColors.Success, "YOLO"]);

            Assert.Equal(Visibility.Visible, badge.Visibility);
            Assert.Equal("KI aktiv | YOLO", text.Text);
            Assert.Equal(PlayerStatusColors.Success, Assert.IsType<SolidColorBrush>(dot.Fill).Color);
        });
    }

    [Fact]
    public void ShowYoloStatus_sets_status_model_and_dot_color()
    {
        RunOnStaThread(() =>
        {
            var bar = new Border { Visibility = Visibility.Collapsed };
            var status = new TextBlock();
            var dot = new Ellipse();
            var model = new TextBlock();
            var show = FindMethod(
                "ShowYoloStatus",
                typeof(FrameworkElement),
                typeof(TextBlock),
                typeof(Shape),
                typeof(TextBlock),
                typeof(string),
                typeof(Color),
                typeof(string));
            Assert.NotNull(show);

            show.Invoke(null, [bar, status, dot, model, "Bereit", PlayerStatusColors.Success, "yolo11"]);

            Assert.Equal(Visibility.Visible, bar.Visibility);
            Assert.Equal("YOLO: Bereit", status.Text);
            Assert.Equal("yolo11", model.Text);
            Assert.Equal(PlayerStatusColors.Success, Assert.IsType<SolidColorBrush>(dot.Fill).Color);
        });
    }

    [Fact]
    public void ShowCodingAiState_sets_status_stage_and_dot_color()
    {
        RunOnStaThread(() =>
        {
            var status = new TextBlock();
            var stage = new TextBlock();
            var dot = new Ellipse();
            var show = FindMethod(
                "ShowCodingAiState",
                typeof(TextBlock),
                typeof(TextBlock),
                typeof(Shape),
                typeof(string),
                typeof(Color),
                typeof(string));
            Assert.NotNull(show);

            show.Invoke(null, [status, stage, dot, "KI bereit", PlayerStatusColors.Warning, "Schritt 2"]);

            Assert.Equal("KI bereit", status.Text);
            Assert.Equal("Schritt 2", stage.Text);
            Assert.Equal(PlayerStatusColors.Warning, Assert.IsType<SolidColorBrush>(dot.Fill).Color);
        });
    }

    [Fact]
    public void ShowDetectionStatus_collapses_summary_without_findings()
    {
        RunOnStaThread(() =>
        {
            var status = new TextBlock();
            var summaryPanel = new Border { Visibility = Visibility.Visible };
            var summary = new TextBlock { Text = "alt" };
            var show = FindMethod(
                "ShowDetectionStatus",
                typeof(TextBlock),
                typeof(FrameworkElement),
                typeof(TextBlock),
                typeof(LiveDetection));
            Assert.NotNull(show);

            var result = new LiveDetection(8, [], null, null);

            show.Invoke(null, [status, summaryPanel, summary, result]);

            Assert.Equal(LiveDetectionDisplayPolicy.BuildDetectionStatusText(result), status.Text);
            Assert.Equal(Visibility.Collapsed, summaryPanel.Visibility);
        });
    }

    [Fact]
    public void ShowDetectionStatus_shows_summary_for_findings()
    {
        RunOnStaThread(() =>
        {
            var status = new TextBlock();
            var summaryPanel = new Border { Visibility = Visibility.Collapsed };
            var summary = new TextBlock();
            var result = new LiveDetection(
                4.25,
                [new LiveFrameFinding("Riss", 3, "3", 20)],
                null,
                null);
            var show = FindMethod(
                "ShowDetectionStatus",
                typeof(TextBlock),
                typeof(FrameworkElement),
                typeof(TextBlock),
                typeof(LiveDetection));
            Assert.NotNull(show);

            show.Invoke(null, [status, summaryPanel, summary, result]);

            Assert.Equal(Visibility.Visible, summaryPanel.Visibility);
            Assert.Contains("Riss", summary.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ShowStoppedDetectionStatus_hides_badge_and_summary_then_shows_stop_text()
    {
        RunOnStaThread(() =>
        {
            var badge = new Border { Visibility = Visibility.Visible };
            var summaryPanel = new Border { Visibility = Visibility.Visible };
            var status = new TextBlock { Visibility = Visibility.Collapsed };
            var show = FindMethod(
                "ShowStoppedDetectionStatus",
                typeof(FrameworkElement),
                typeof(FrameworkElement),
                typeof(TextBlock),
                typeof(int));
            Assert.NotNull(show);

            show.Invoke(null, [badge, summaryPanel, status, 7]);

            Assert.Equal(Visibility.Collapsed, badge.Visibility);
            Assert.Equal(Visibility.Collapsed, summaryPanel.Visibility);
            Assert.Equal("KI-Analyse beendet — 7 Beobachtungen", status.Text);
            Assert.Equal(Visibility.Visible, status.Visibility);
        });
    }

    [Fact]
    public void HideDetectionStatus_collapses_status_text()
    {
        RunOnStaThread(() =>
        {
            var status = new TextBlock { Visibility = Visibility.Visible };
            var hide = FindMethod("HideDetectionStatus", typeof(TextBlock));
            Assert.NotNull(hide);

            hide.Invoke(null, [status]);

            Assert.Equal(Visibility.Collapsed, status.Visibility);
        });
    }

    [Fact]
    public void ShowWaitingForFrame_shows_waiting_status()
    {
        RunOnStaThread(() =>
        {
            var status = new TextBlock { Visibility = Visibility.Collapsed, Text = "alt" };
            var show = FindMethod("ShowWaitingForFrame", typeof(TextBlock));
            Assert.NotNull(show);

            show.Invoke(null, [status]);

            Assert.Equal("Warte auf Frame...", status.Text);
            Assert.Equal(Visibility.Visible, status.Visibility);
        });
    }

    [Fact]
    public void ShowDetectionError_sets_error_text_without_changing_visibility()
    {
        RunOnStaThread(() =>
        {
            var status = new TextBlock { Visibility = Visibility.Collapsed };
            var show = FindMethod("ShowDetectionError", typeof(TextBlock), typeof(string));
            Assert.NotNull(show);

            show.Invoke(null, [status, "Timeout"]);

            Assert.Equal("Fehler: Timeout", status.Text);
            Assert.Equal(Visibility.Collapsed, status.Visibility);
        });
    }

    [Fact]
    public void ShowPipelineHealthDetails_sets_all_health_lines()
    {
        RunOnStaThread(() =>
        {
            var sidecar = new TextBlock();
            var token = new TextBlock();
            var yolo = new TextBlock();
            var dino = new TextBlock();
            var sam = new TextBlock();
            var mode = new TextBlock();
            var details = new PipelineHealthDetailsUiState(
                "Sidecar: OK",
                "Token: OK",
                "YOLO: geladen",
                "DINO: geladen",
                "SAM: geladen",
                "Modus: Multi-Model");
            var show = FindMethod(
                "ShowPipelineHealthDetails",
                typeof(TextBlock),
                typeof(TextBlock),
                typeof(TextBlock),
                typeof(TextBlock),
                typeof(TextBlock),
                typeof(TextBlock),
                typeof(PipelineHealthDetailsUiState));
            Assert.NotNull(show);

            show.Invoke(null, [sidecar, token, yolo, dino, sam, mode, details]);

            Assert.Equal("Sidecar: OK", sidecar.Text);
            Assert.Equal("Token: OK", token.Text);
            Assert.Equal("YOLO: geladen", yolo.Text);
            Assert.Equal("DINO: geladen", dino.Text);
            Assert.Equal("SAM: geladen", sam.Text);
            Assert.Equal("Modus: Multi-Model", mode.Text);
        });
    }

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(PlayerStatusColors).Assembly
            .GetType("AuswertungPro.Next.UI.Views.Windows.LiveDetectionStatusControls")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);

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
