using System.Windows.Controls;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SanierungsMatrixPage : UserControl
{
    public SanierungsMatrixPage()
    {
        InitializeComponent();
    }

    private void MeasureComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBox combo || !combo.IsEnabled || combo.IsDropDownOpen)
            return;

        combo.Focus();
        combo.IsDropDownOpen = true;
        e.Handled = true;
    }
}
