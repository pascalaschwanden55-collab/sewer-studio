using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingEventListItemControls
{
    public static void Apply(
        Ellipse? zoneDot,
        TextBlock? confidenceText,
        TextBlock? statusIcon,
        CodingEvent codingEvent)
    {
        var status = DefectStatusPolicy.GetStatus(codingEvent);

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
