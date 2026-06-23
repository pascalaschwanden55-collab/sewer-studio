using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetLiveDetectionBadge(status, dotColor, stage));
            return;
        }

        var stageSuffix = string.IsNullOrWhiteSpace(stage) ? string.Empty : $" | {stage}";
        AiStatusBadge.Visibility = Visibility.Visible;
        AiStatusText.Text = $"{status}{stageSuffix}";
        AiStatusDot.Fill = new SolidColorBrush(dotColor);
    }

    private void SetYoloStatus(string text, Color dotColor, string? model = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetYoloStatus(text, dotColor, model));
            return;
        }

        YoloStatusBar.Visibility = Visibility.Visible;
        TxtYoloStatus.Text = $"YOLO: {text}";
        YoloDot.Fill = new SolidColorBrush(dotColor);
        TxtYoloModel.Text = model ?? string.Empty;
    }

    private void SetCodingAiState(string status, Color dotColor, string? stage = null, bool pulse = false)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetCodingAiState(status, dotColor, stage, pulse));
            return;
        }

        TxtCodingAiStatus.Text = status;
        TxtCodingAiStage.Text = stage ?? string.Empty;
        CodingAiDot.Fill = new SolidColorBrush(dotColor);
        if (pulse)
            StartCodingAiPulse();
        else
            StopCodingAiPulse();
    }

    private void StartCodingAiPulse()
    {
        if (_codingAiPulseRunning)
            return;

        _codingAiPulseRunning = true;
        CodingAiPulseRing.Opacity = 1.0;
        if (CodingAiPulseRing.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            CodingAiPulseRing.RenderTransform = scale;
        }

        var scaleAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 2.2,
            Duration = TimeSpan.FromMilliseconds(900),
            RepeatBehavior = RepeatBehavior.Forever
        };
        var opacityAnim = new DoubleAnimation
        {
            From = 0.75,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(900),
            RepeatBehavior = RepeatBehavior.Forever
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        CodingAiPulseRing.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }

    private void StopCodingAiPulse()
    {
        _codingAiPulseRunning = false;

        if (CodingAiPulseRing.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        CodingAiPulseRing.BeginAnimation(UIElement.OpacityProperty, null);
        CodingAiPulseRing.Opacity = 0;
    }
}
