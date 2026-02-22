using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class MeasureSelectionWindow : Window
{
    public MeasureSelectionWindow()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
