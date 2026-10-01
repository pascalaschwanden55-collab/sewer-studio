using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingEventListItemControls
{
    public static void Apply(
        Ellipse? zoneDot,
        TextBlock? confidenceText,
        TextBlock? statusIcon,
        CodingEvent codingEvent)
        => Apply(zoneDot, confidenceText, statusIcon, null, null, codingEvent, null);

    /// <summary>
    /// Zusaetzlich zur Statusfarbe wird die Meterangabe gesetzt: Punktschaden als
    /// einzelner Wert, Streckenschaden als Von-Bis, ein offener Anfang klar als offen.
    /// Dafuer sind die uebrigen Ereignisse noetig, weil die Endmarke eines
    /// Streckenschadens nur ueber ihren Anfang erkennbar ist.
    /// </summary>
    public static void Apply(
        Ellipse? zoneDot,
        TextBlock? confidenceText,
        TextBlock? statusIcon,
        TextBlock? meterText,
        Border? stretchBadge,
        CodingEvent codingEvent,
        IReadOnlyList<CodingEvent>? allEvents)
    {
        var status = DefectStatusPolicy.GetStatus(codingEvent);

        if (meterText is not null)
            meterText.Text = CodingStretchDamageDisplayPolicy.BuildMeterText(codingEvent, allEvents);

        if (stretchBadge is not null)
        {
            var badge = CodingStretchDamageDisplayPolicy.BuildBadgeText(codingEvent, allEvents);
            stretchBadge.Visibility = string.IsNullOrEmpty(badge)
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (stretchBadge.Child is TextBlock badgeText)
                badgeText.Text = badge;
        }

        if (zoneDot is not null)
            zoneDot.Fill = new SolidColorBrush(CodingDefectStatusDisplayPolicy.ZoneDotColor(status));

        if (confidenceText is not null)
        {
            if (codingEvent.AiContext is not null)
            {
                confidenceText.Text = $"{codingEvent.AiContext.Confidence * 100:F0}%";
                confidenceText.Foreground = CodingSessionViewModel.GetConfidenceBrush(codingEvent.AiContext.Confidence);
            }
            else
            {
                confidenceText.Text = "";
            }
        }

        if (statusIcon is not null)
        {
            statusIcon.Text = CodingDefectStatusDisplayPolicy.StatusIcon(status);
            statusIcon.Foreground = CodingSessionViewModel.GetStatusBrush(status);
        }
    }
}
