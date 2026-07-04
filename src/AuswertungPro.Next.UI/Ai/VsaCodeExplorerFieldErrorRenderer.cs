using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public static class VsaCodeExplorerFieldErrorRenderer
{
    public static void Apply(VsaCodeExplorerFieldErrorPresentation presentation, TextBlock target)
    {
        target.Text = presentation.Text;
        target.Visibility = presentation.Show ? Visibility.Visible : Visibility.Collapsed;
    }
}
