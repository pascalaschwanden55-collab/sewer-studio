using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DataPage
{
    private void Grid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (Grid.SelectedCells.Count > 0)
            _activeColumn = Grid.SelectedCells[0].Column;
        UpdateAlignmentButtonsForCurrentColumn();
    }

    private void Grid_CurrentCellChanged(object sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (Grid.CurrentCell.Column is not null)
            _activeColumn = Grid.CurrentCell.Column;
        UpdateAlignmentButtonsForCurrentColumn();
    }

    private void Grid_ColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        if (e.OriginalSource is not DependencyObject dep)
            return;

        var header = FindAncestor<DataGridColumnHeader>(dep);
        if (header?.Column is null)
            return;

        _activeColumn = header.Column;
        TrySetCurrentCellForColumn(_activeColumn);
        UpdateAlignmentButtonsForCurrentColumn();
    }

    private void Grid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        _ = sender;
        _ = e;
        QueueLayoutSave();
    }

    private void AlignLeftButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyHorizontalAlignmentToCurrentColumn(HorizontalAlignment.Left);
    }

    private void AlignCenterButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyHorizontalAlignmentToCurrentColumn(HorizontalAlignment.Center);
    }

    private void AlignRightButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyHorizontalAlignmentToCurrentColumn(HorizontalAlignment.Right);
    }

    private void AlignTopButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyVerticalAlignmentToCurrentColumn(VerticalAlignment.Top);
    }

    private void AlignMiddleButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyVerticalAlignmentToCurrentColumn(VerticalAlignment.Center);
    }

    private void AlignBottomButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyVerticalAlignmentToCurrentColumn(VerticalAlignment.Bottom);
    }

    private void ApplyHorizontalAlignmentToCurrentColumn(HorizontalAlignment horizontalAlignment)
    {
        if (_updatingAlignmentButtons)
            return;

        var column = GetActiveColumn();
        if (column is null)
            return;

        var verticalAlignment = GetColumnVerticalAlignment(column);
        ApplyColumnAlignment(column, horizontalAlignment, verticalAlignment);
        UpdateAlignmentButtonsForCurrentColumn();
    }

    private void ApplyVerticalAlignmentToCurrentColumn(VerticalAlignment verticalAlignment)
    {
        if (_updatingAlignmentButtons)
            return;

        var column = GetActiveColumn();
        if (column is null)
            return;

        var horizontalAlignment = GetColumnHorizontalAlignment(column);
        ApplyColumnAlignment(column, horizontalAlignment, verticalAlignment);
        UpdateAlignmentButtonsForCurrentColumn();
    }

    private DataGridColumn? GetActiveColumn()
    {
        if (_activeColumn is not null)
            return _activeColumn;

        if (Grid.CurrentCell.Column is not null)
            return Grid.CurrentCell.Column;

        if (Grid.SelectedCells.Count > 0)
            return Grid.SelectedCells[0].Column;

        return null;
    }

    private HorizontalAlignment GetColumnHorizontalAlignment(DataGridColumn column)
    {
        return _columnLayoutController.GetHorizontalAlignment(column);
    }

    private VerticalAlignment GetColumnVerticalAlignment(DataGridColumn column)
    {
        return _columnLayoutController.GetVerticalAlignment(column);
    }

    private void TrySetCurrentCellForColumn(DataGridColumn column)
    {
        var rowItem = Grid.SelectedItem ?? Grid.Items.Cast<object>().FirstOrDefault();
        if (rowItem is null)
            return;

        Grid.CurrentCell = new DataGridCellInfo(rowItem, column);
    }

    private void ApplyColumnAlignment(DataGridColumn column, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
    {
        _columnLayoutController.SetAlignment(column, horizontalAlignment, verticalAlignment);
    }

    private void UpdateAlignmentButtonsForCurrentColumn()
    {
        _updatingAlignmentButtons = true;
        try
        {
            var column = GetActiveColumn();
            if (column is null)
            {
                SetAlignmentButtonsUnchecked();
                return;
            }

            var horizontal = GetColumnHorizontalAlignment(column);
            var vertical = GetColumnVerticalAlignment(column);

            AlignLeftButton.IsChecked = horizontal == HorizontalAlignment.Left;
            AlignCenterButton.IsChecked = horizontal == HorizontalAlignment.Center;
            AlignRightButton.IsChecked = horizontal == HorizontalAlignment.Right;

            AlignTopButton.IsChecked = vertical == VerticalAlignment.Top;
            AlignMiddleButton.IsChecked = vertical == VerticalAlignment.Center;
            AlignBottomButton.IsChecked = vertical == VerticalAlignment.Bottom;
        }
        finally
        {
            _updatingAlignmentButtons = false;
        }
    }

    private void SetAlignmentButtonsUnchecked()
    {
        AlignLeftButton.IsChecked = false;
        AlignCenterButton.IsChecked = false;
        AlignRightButton.IsChecked = false;
        AlignTopButton.IsChecked = false;
        AlignMiddleButton.IsChecked = false;
        AlignBottomButton.IsChecked = false;
    }

    private void RestoreLayoutFromSettings()
    {
        var sp = Services;
        var layout = sp.Settings.DataPageLayout;
        _columnLayoutController.Restore(Grid.Columns, layout);
    }

    private void QueueLayoutSave()
    {
        if (_columnLayoutController.IsRestoring)
            return;

        _layoutSaveDebounceTimer.Stop();
        _layoutSaveDebounceTimer.Start();
    }

    private void SaveLayoutToSettings()
    {
        // Beim Entladen der Seite (Unloaded-Handler) kann der DataContext bereits
        // null sein. Dann gibt es nichts zu speichern — kein Zugriff auf Vm/Services
        // erzwingen (wuerde sonst werfen).
        if (_columnLayoutController.IsRestoring || Grid.Columns.Count == 0
            || DataContext is not AuswertungPro.Next.UI.ViewModels.Pages.DataPageViewModel)
            return;

        var sp = Services;
        var layout = sp.Settings.DataPageLayout ?? new DataPageLayoutSettings();
        layout.Columns = _columnLayoutController.Capture(Grid.Columns).Columns;
        sp.Settings.DataPageLayout = layout;
        sp.Settings.Save();
    }
}
