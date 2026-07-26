using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingConfirmationPanelControls
{
    private readonly Border CodingConfirmationPanel;
    private readonly Shape ConfirmAmpel;
    private readonly TextBlock TxtConfirmCode;
    private readonly TextBlock TxtConfirmConfidence;
    private readonly TextBlock TxtConfirmDescription;
    private readonly TextBlock TxtConfirmDetail;
    private readonly FrameworkElement ConfirmSaveErrorPanel;
    private readonly TextBlock TxtConfirmSaveError;

    public CodingConfirmationPanelControls(
        Border codingConfirmationPanel,
        Shape confirmAmpel,
        TextBlock txtConfirmCode,
        TextBlock txtConfirmConfidence,
        TextBlock txtConfirmDescription,
        TextBlock txtConfirmDetail,
        FrameworkElement confirmSaveErrorPanel,
        TextBlock txtConfirmSaveError)
    {
        CodingConfirmationPanel = codingConfirmationPanel;
        ConfirmAmpel = confirmAmpel;
        TxtConfirmCode = txtConfirmCode;
        TxtConfirmConfidence = txtConfirmConfidence;
        TxtConfirmDescription = txtConfirmDescription;
        TxtConfirmDetail = txtConfirmDetail;
        ConfirmSaveErrorPanel = confirmSaveErrorPanel;
        TxtConfirmSaveError = txtConfirmSaveError;
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
        ConfirmSaveErrorPanel.Visibility = Visibility.Collapsed;
        CodingConfirmationPanel.Visibility = Visibility.Visible;
        return ampelColor;
    }

    /// <summary>
    /// Zeigt einen fehlgeschlagenen Goldsave im Panel an: Das Panel BLEIBT offen,
    /// der Fehlertext und die „Erneut speichern"-Schaltflaeche werden eingeblendet.
    /// </summary>
    public void ShowPersistenceError(string? error)
    {
        TxtConfirmSaveError.Text = string.IsNullOrWhiteSpace(error)
            ? "Unbekannter Fehler."
            : error;
        ConfirmSaveErrorPanel.Visibility = Visibility.Visible;
        CodingConfirmationPanel.Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        ConfirmSaveErrorPanel.Visibility = Visibility.Collapsed;
        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
    }
}
