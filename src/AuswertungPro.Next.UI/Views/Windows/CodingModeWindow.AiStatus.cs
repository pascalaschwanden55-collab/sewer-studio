using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow KI-Status-Anzeige (Slice 8a.2.4): Status-Text,
// Status-Dot mit Pulse-Animation, Pipeline-Dot-Farben (YOLO/Qwen/Tracker)
// und Model-Namen-Kompaktierung. Reine UI-State-Helfer — keine
// Geschaeftslogik. Aus dem Hauptdatei extrahiert.
public partial class CodingModeWindow
{
    private static string CompactModelName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return "?";

        var trimmed = model.Trim();
        var slashIndex = trimmed.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < trimmed.Length - 1)
            trimmed = trimmed[(slashIndex + 1)..];
        return trimmed;
    }

    private void SetPipelineDots(string? stage, bool busy, bool error)
    {
        var gray = Color.FromRgb(0x94, 0xA3, 0xB8);
        var amber = Color.FromRgb(0xF5, 0x9E, 0x0B);
        var green = Color.FromRgb(0x22, 0xC5, 0x5E);
        var red = Color.FromRgb(0xEF, 0x44, 0x44);
        var blue = Color.FromRgb(0x38, 0xBD, 0xF8);

        Color yolo = gray;
        Color qwen = busy ? amber : green;
        Color tracker = gray;

        if (error)
        {
            qwen = red;
            tracker = red;
        }
        else if (!string.IsNullOrWhiteSpace(stage) &&
                 stage.Contains("Overlay", StringComparison.OrdinalIgnoreCase))
        {
            tracker = busy ? amber : green;
        }
        else if (!string.IsNullOrWhiteSpace(stage) &&
                 stage.Contains("Snapshot", StringComparison.OrdinalIgnoreCase))
        {
            tracker = blue;
        }

        DotYolo.Fill = new SolidColorBrush(yolo);
        DotQwen.Fill = new SolidColorBrush(qwen);
        DotTracker.Fill = new SolidColorBrush(tracker);
    }

    private void StartAiStatusPulse()
    {
        if (_aiStatusPulseRunning)
            return;

        _aiStatusPulseRunning = true;
        var anim = new DoubleAnimation
        {
            From = 0.35,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(600),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        AiStatusDot.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private void StopAiStatusPulse()
    {
        _aiStatusPulseRunning = false;
        AiStatusDot.BeginAnimation(UIElement.OpacityProperty, null);
        AiStatusDot.Opacity = 1.0;
    }

    private void SetAiStatus(
        string text,
        string dotColorHex,
        string? stage = null,
        bool busy = false,
        bool error = false)
    {
        TxtAiStatus.Text = text;
        var color = (Color)ColorConverter.ConvertFromString(dotColorHex);
        AiStatusDot.Fill = new SolidColorBrush(color);
        var model = CompactModelName(_aiModelName);
        TxtAiModel.Text = string.IsNullOrWhiteSpace(stage)
            ? model
            : $"{model} | {stage}";
        SetPipelineDots(stage, busy, error);
        if (busy)
            StartAiStatusPulse();
        else
            StopAiStatusPulse();
    }
}
