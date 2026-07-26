using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class LiveDetectionStatusControls
{
    public static void ShowLiveDetectionBadge(
        FrameworkElement badge,
        TextBlock statusText,
        Shape dot,
        string status,
        Color dotColor,
        string? stage = null)
    {
        ArgumentNullException.ThrowIfNull(badge);
        ArgumentNullException.ThrowIfNull(statusText);
        ArgumentNullException.ThrowIfNull(dot);

        var stageSuffix = string.IsNullOrWhiteSpace(stage) ? string.Empty : $" | {stage}";
        badge.Visibility = Visibility.Visible;
        statusText.Text = $"{status}{stageSuffix}";
        dot.Fill = new SolidColorBrush(dotColor);
    }

    public static void ShowYoloStatus(
        FrameworkElement statusBar,
        TextBlock statusText,
        Shape dot,
        TextBlock modelText,
        string text,
        Color dotColor,
        string? model = null)
    {
        ArgumentNullException.ThrowIfNull(statusBar);
        ArgumentNullException.ThrowIfNull(statusText);
        ArgumentNullException.ThrowIfNull(dot);
        ArgumentNullException.ThrowIfNull(modelText);

        statusBar.Visibility = Visibility.Visible;
        statusText.Text = $"YOLO: {text}";
        dot.Fill = new SolidColorBrush(dotColor);
        modelText.Text = model ?? string.Empty;
    }

    public static void ShowCodingAiState(
        TextBlock statusText,
        TextBlock stageText,
        Shape dot,
        string status,
        Color dotColor,
        string? stage = null)
    {
        ArgumentNullException.ThrowIfNull(statusText);
        ArgumentNullException.ThrowIfNull(stageText);
        ArgumentNullException.ThrowIfNull(dot);

        statusText.Text = status;
        stageText.Text = stage ?? string.Empty;
        dot.Fill = new SolidColorBrush(dotColor);
    }

    public static void ShowDetectionStatus(
        TextBlock statusText,
        FrameworkElement summaryPanel,
        TextBlock summaryText,
        LiveDetection result)
    {
        ArgumentNullException.ThrowIfNull(statusText);
        ArgumentNullException.ThrowIfNull(summaryPanel);
        ArgumentNullException.ThrowIfNull(summaryText);
        ArgumentNullException.ThrowIfNull(result);

        statusText.Text = LiveDetectionDisplayPolicy.BuildDetectionStatusText(result);
        if (result.Error is not null)
            return;

        if (result.Findings.Count > 0)
        {
            summaryPanel.Visibility = Visibility.Visible;
            summaryText.Text = LiveDetectionDisplayPolicy.BuildFindingSummaryText(result.Findings);
        }
        else
        {
            summaryPanel.Visibility = Visibility.Collapsed;
        }
    }

    public static void ShowDetectionConfirmation(
        FrameworkElement confirmationPanel,
        TextBlock findingText,
        TextBlock detailText,
        IReadOnlyList<LiveFrameFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(confirmationPanel);
        ArgumentNullException.ThrowIfNull(findingText);
        ArgumentNullException.ThrowIfNull(detailText);
        ArgumentNullException.ThrowIfNull(findings);

        findingText.Text = LiveDetectionDisplayPolicy.BuildDetectionConfirmationTitle(findings);
        detailText.Text = LiveDetectionDisplayPolicy.BuildDetectionConfirmationDetails(findings);
        confirmationPanel.Visibility = Visibility.Visible;
    }

    public static void HideDetectionConfirmation(FrameworkElement confirmationPanel)
    {
        ArgumentNullException.ThrowIfNull(confirmationPanel);

        confirmationPanel.Visibility = Visibility.Collapsed;
    }

    public static void ShowStoppedDetectionStatus(
        FrameworkElement badge,
        FrameworkElement summaryPanel,
        TextBlock statusText,
        int totalEvents)
    {
        ArgumentNullException.ThrowIfNull(badge);
        ArgumentNullException.ThrowIfNull(summaryPanel);
        ArgumentNullException.ThrowIfNull(statusText);

        badge.Visibility = Visibility.Collapsed;
        summaryPanel.Visibility = Visibility.Collapsed;
        statusText.Text = $"KI-Analyse beendet — {totalEvents} Beobachtungen";
        statusText.Visibility = Visibility.Visible;
    }

    public static void HideDetectionStatus(TextBlock statusText)
    {
        ArgumentNullException.ThrowIfNull(statusText);

        statusText.Visibility = Visibility.Collapsed;
    }

    public static void SetDetectionStatusVisibility(TextBlock statusText, bool isVisible)
    {
        ArgumentNullException.ThrowIfNull(statusText);

        statusText.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public static void ShowWaitingForFrame(TextBlock statusText)
    {
        ArgumentNullException.ThrowIfNull(statusText);

        statusText.Text = "Warte auf Frame...";
        statusText.Visibility = Visibility.Visible;
    }

    public static void ShowDetectionError(TextBlock statusText, string message)
    {
        ArgumentNullException.ThrowIfNull(statusText);

        statusText.Text = $"Fehler: {message}";
    }

    public static void ShowStatusMessage(TextBlock statusText, string message)
    {
        ArgumentNullException.ThrowIfNull(statusText);

        statusText.Text = message;
        statusText.Visibility = Visibility.Visible;
    }

    public static void ShowPipelineHealthDetails(
        TextBlock sidecar,
        TextBlock token,
        TextBlock yolo,
        TextBlock dino,
        TextBlock sam,
        TextBlock mode,
        PipelineHealthDetailsUiState details)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(yolo);
        ArgumentNullException.ThrowIfNull(dino);
        ArgumentNullException.ThrowIfNull(sam);
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(details);

        sidecar.Text = details.Sidecar;
        token.Text = details.Token;
        yolo.Text = details.Yolo;
        dino.Text = details.Dino;
        sam.Text = details.Sam;
        mode.Text = details.Mode;
    }
}
