using System;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class MeasureTemplateEditorWindow : Window
{
    private Point _startPoint;

    public MeasureTemplateEditorWindow()
    {
        InitializeComponent();
    }

    private void PriceItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MeasureTemplateEditorViewModel vm) return;
        
        var grid = sender as System.Windows.Controls.DataGrid;
        if (grid?.SelectedItem is CatalogItemRow priceRow)
        {
            vm.AddLineCommand.Execute(priceRow);
        }
    }

    private void AvailablePrices_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
    }

    private void AvailablePrices_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        if (DataContext is not MeasureTemplateEditorViewModel)
            return;

        var mousePos = e.GetPosition(null);
        var diff = _startPoint - mousePos;
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is System.Windows.Controls.DataGrid grid &&
            grid.SelectedItem is CatalogItemRow priceRow)
        {
            DragDrop.DoDragDrop(grid, priceRow, DragDropEffects.Copy);
        }
    }

    private void TemplateLines_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MeasureTemplateEditorViewModel vm)
            return;
        if (!e.Data.GetDataPresent(typeof(CatalogItemRow)))
            return;

        if (e.Data.GetData(typeof(CatalogItemRow)) is CatalogItemRow priceRow)
        {
            if (vm.AddLineCommand.CanExecute(priceRow))
                vm.AddLineCommand.Execute(priceRow);
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void TemplateLines_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(CatalogItemRow))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }
}
