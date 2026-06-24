using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingConfirmationPanelControls
{
    private readonly Border CodingConfirmationPanel;
    private readonly Shape ConfirmAmpel;
    private readonly TextBlock TxtConfirmCode;
    private readonly TextBlock TxtConfirmConfidence;
    private readonly TextBlock TxtConfirmDescription;
    private readonly TextBlock TxtConfirmDetail;

    public CodingConfirmationPanelControls(
        Border codingConfirmationPanel,
        Shape confirmAmpel,
        TextBlock txtConfirmCode,
        TextBlock txtConfirmConfidence,
        TextBlock txtConfirmDescription,
        TextBlock txtConfirmDetail)
    {
        CodingConfirmationPanel = codingConfirmationPanel;
        ConfirmAmpel = confirmAmpel;
        TxtConfirmCode = txtConfirmCode;
        TxtConfirmConfidence = txtConfirmConfidence;
        TxtConfirmDescription = txtConfirmDescription;
        TxtConfirmDetail = txtConfirmDetail;
    }

    public Color Apply(CodingEvent codingEvent, QualityGateResult gateResult)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);
        ArgumentNullException.ThrowIfNull(gateResult);

        var ampelColor = CodingConfirmationDisplayPolicy.AmpelColor(gateResult);
        ConfirmAmpel.Fill = new SolidColorBrush(ampelColor);
        TxtConfirmCode.Text = codingEvent.Entry.Code ?? "???";
        TxtConfirmConfidence.Text = $"({gateResult.CompositeConfidence:P0})";
        TxtConfirmDescription.Text = codingEvent.Entry.Beschreibung ?? codingEvent.AiContext?.Reason ?? "";
        TxtConfirmDetail.Text = CodingConfirmationDisplayPolicy.ConfirmationDetail(gateResult);
        CodingConfirmationPanel.Visibility = Visibility.Visible;
        return ampelColor;
    }

    public void Hide()
    {
        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
    }
}
