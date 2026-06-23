using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CmbEingabemarker_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelEingabemarker();
            ClearDetectionOverlays();
            return;
        }

        if (e.Key != Key.Enter) return;
        SubmitEingabemarker().SafeFireAndForget("SubmitEingabemarker");
    }

    private void CmbEingabemarker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EingabemarkerPopup.Visibility != Visibility.Visible) return;
        if (CmbEingabemarker.SelectedItem is ComboBoxItem item && item.Content is string text && !string.IsNullOrEmpty(text))
        {
            TxtEingabemarker.Text = text;
            SubmitEingabemarker().SafeFireAndForget("SubmitEingabemarker");
        }
    }

    private static string? ResolveEingabemarkerCodeHint(string? keyword)
        => AuswertungPro.Next.UI.Player.PlayerVsaCodeHintResolver.ResolveKeyword(keyword);
}
