using System.Windows;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class HydraulikPrintDialog : Window
{
    public HydraulikPrintOptions? SelectedOptions { get; private set; }

    public HydraulikPrintDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SelectedOptions = new HydraulikPrintOptions
        {
            IncludeTeilfuellung = ChkTeilfuellung.IsChecked == true,
            IncludeVollfuellung = ChkVollfuellung.IsChecked == true,
            IncludeKennzahlen = ChkKennzahlen.IsChecked == true,
            IncludeAblagerung = ChkAblagerung.IsChecked == true,
            IncludeAuslastung = ChkAuslastung.IsChecked == true,
            IncludeBewertung = ChkBewertung.IsChecked == true,
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
