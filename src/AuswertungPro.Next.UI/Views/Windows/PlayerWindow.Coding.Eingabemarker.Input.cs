using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CmbEingabemarker_KeyDown(object sender, KeyEventArgs e)
    {
        CodingEingabemarkerKeyInputWorkflow.Execute(
            new CodingEingabemarkerKeyInputWorkflowRequest(
                IsEscape: e.Key == Key.Escape,
                IsEnter: e.Key == Key.Enter),
            new CodingEingabemarkerKeyInputWorkflowActions(
                CancelMarker: () => _codingEingabemarkerInteractionController.Cancel(),
                ClearDetectionOverlays: ClearDetectionOverlays,
                Submit: () => SubmitEingabemarker().SafeFireAndForget("SubmitEingabemarker")));
    }

    private void CmbEingabemarker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CodingEingabemarkerSelectionInputWorkflow.Execute(
            new CodingEingabemarkerSelectionInputWorkflowRequest(
                IsPopupVisible: CodingEingabemarkerPopupControls.IsVisible(EingabemarkerPopup),
                SelectedText: CodingEingabemarkerPopupControls.ResolveSelectedText(CmbEingabemarker.SelectedItem)),
            new CodingEingabemarkerSelectionInputWorkflowActions(
                ApplyQuickSelection: text => CodingEingabemarkerPopupControls.ApplyQuickSelection(
                    TxtEingabemarker,
                    text),
                Submit: () => SubmitEingabemarker().SafeFireAndForget("SubmitEingabemarker")));
    }

    private static string? ResolveEingabemarkerCodeHint(string? keyword)
        => AuswertungPro.Next.UI.Player.PlayerVsaCodeHintResolver.ResolveKeyword(keyword);
}
