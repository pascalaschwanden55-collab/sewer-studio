using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed record DataGridColumnAlignmentButtons(
    ToggleButton Left,
    ToggleButton Center,
    ToggleButton Right,
    ToggleButton Top,
    ToggleButton Middle,
    ToggleButton Bottom);

public sealed class DataGridColumnAlignmentToolbar
{
    private readonly DataGrid _grid;
    private readonly DataGridColumnLayoutController _layoutController;
    private readonly DataGridColumnAlignmentButtons _buttons;
    private bool _updatingButtons;
    private DataGridColumn? _activeColumn;

    public DataGridColumnAlignmentToolbar(
        DataGrid grid,
        DataGridColumnLayoutController layoutController,
        DataGridColumnAlignmentButtons buttons)
    {
        _grid = grid;
        _layoutController = layoutController;
        _buttons = buttons;
    }

    public void ClearActiveColumn()
    {
        _activeColumn = null;
    }

    public void TrackSelectedCells()
    {
        if (_grid.SelectedCells.Count > 0)
            _activeColumn = _grid.SelectedCells[0].Column;

        UpdateButtons();
    }

    public void TrackCurrentCell()
    {
        if (_grid.CurrentCell.Column is not null)
            _activeColumn = _grid.CurrentCell.Column;

        UpdateButtons();
    }

    public void TrackHeaderClick(DependencyObject source)
    {
        var header = VisualTreeSafe.FindAncestor<DataGridColumnHeader>(source);
        if (header?.Column is null)
            return;

        _activeColumn = header.Column;
        TrySetCurrentCellForColumn(_activeColumn);
        UpdateButtons();
    }

    public void ApplyHorizontalAlignment(HorizontalAlignment horizontalAlignment)
    {
        if (_updatingButtons)
            return;

        var column = GetActiveColumn();
        if (column is null)
            return;

        var verticalAlignment = _layoutController.GetVerticalAlignment(column);
        SetAlignment(column, horizontalAlignment, verticalAlignment);
        UpdateButtons();
    }

    public void ApplyVerticalAlignment(VerticalAlignment verticalAlignment)
    {
        if (_updatingButtons)
            return;

        var column = GetActiveColumn();
        if (column is null)
            return;

        var horizontalAlignment = _layoutController.GetHorizontalAlignment(column);
        SetAlignment(column, horizontalAlignment, verticalAlignment);
        UpdateButtons();
    }

    public void SetAlignment(
        DataGridColumn column,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment)
        => _layoutController.SetAlignment(column, horizontalAlignment, verticalAlignment);

    public void UpdateButtons()
    {
        _updatingButtons = true;
        try
        {
            var column = GetActiveColumn();
            if (column is null)
            {
                SetButtonsUnchecked();
                return;
            }

            var horizontal = _layoutController.GetHorizontalAlignment(column);
            var vertical = _layoutController.GetVerticalAlignment(column);

            _buttons.Left.IsChecked = horizontal == HorizontalAlignment.Left;
            _buttons.Center.IsChecked = horizontal == HorizontalAlignment.Center;
            _buttons.Right.IsChecked = horizontal == HorizontalAlignment.Right;

            _buttons.Top.IsChecked = vertical == VerticalAlignment.Top;
            _buttons.Middle.IsChecked = vertical == VerticalAlignment.Center;
            _buttons.Bottom.IsChecked = vertical == VerticalAlignment.Bottom;
        }
        finally
        {
            _updatingButtons = false;
        }
    }

    private DataGridColumn? GetActiveColumn()
    {
        if (_activeColumn is not null)
            return _activeColumn;

        if (_grid.CurrentCell.Column is not null)
            return _grid.CurrentCell.Column;

        if (_grid.SelectedCells.Count > 0)
            return _grid.SelectedCells[0].Column;

        return null;
    }

    private void TrySetCurrentCellForColumn(DataGridColumn column)
    {
        var rowItem = _grid.SelectedItem ?? _grid.Items.Cast<object>().FirstOrDefault();
        if (rowItem is null)
            return;

        _grid.CurrentCell = new DataGridCellInfo(rowItem, column);
    }

    private void SetButtonsUnchecked()
    {
        _buttons.Left.IsChecked = false;
        _buttons.Center.IsChecked = false;
        _buttons.Right.IsChecked = false;
        _buttons.Top.IsChecked = false;
        _buttons.Middle.IsChecked = false;
        _buttons.Bottom.IsChecked = false;
    }
}
