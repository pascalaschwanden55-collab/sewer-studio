using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SanierungsMatrixPage : UserControl
{
    public SanierungsMatrixPage()
    {
        InitializeComponent();
        PhotoHoverPreviewBehavior.SetPhotoPathsSelector(MatrixRowsGrid, PhotoHoverPreviewSelectors.SanierungsMatrixRowPhotos);
        PhotoHoverPreviewBehavior.SetProjectRootProvider(
            MatrixRowsGrid,
            () => DataContext is SanierungsMatrixPageViewModel vm ? vm.ProjectRootPath : null);
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
