using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Dialogs;

public partial class PositionTemplateEditorDialog : Window
{
    private string? _draggedItemKey;
    private Point _startPoint;

    public PositionTemplateEditorDialog(string? projectPath)
    {
        InitializeComponent();
        DataContext = new PositionTemplateEditorViewModel(projectPath, this);
    }
        // Event-Handler für das Löschen einer Position per Button
        private void DeletePosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PositionTemplate pos && DataContext is PositionTemplateEditorViewModel vm && vm.SelectedGroup != null)
            {
                vm.SelectedPosition = pos;
                if (vm.RemovePositionCommand.CanExecute(null))
                    vm.RemovePositionCommand.Execute(null);
            }
        }

    private void PositionsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
    }

    private void PositionsDataGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && DataContext is PositionTemplateEditorViewModel vm)
        {
            var mousePos = e.GetPosition(null);
            var diff = _startPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (vm.SelectedPosition != null)
                {
                    _draggedItemKey = vm.SelectedPosition.ItemKey;
                    var data = new DataObject("PositionTemplateKey", _draggedItemKey);
                    DragDrop.DoDragDrop(PositionsDataGrid, data, DragDropEffects.Move);
                }
            }
        }
    }

    private void StorageListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("PositionTemplateKey") && DataContext is PositionTemplateEditorViewModel vm)
        {
            var key = e.Data.GetData("PositionTemplateKey") as string;
            if (key != null && vm.SelectedGroup != null)
            {
                var pos = vm.SelectedGroup.Positions.FirstOrDefault(p => p.ItemKey == key);
                if (pos != null)
                {
                    var positionCopy = new PositionTemplate
                    {
                        ItemKey = pos.ItemKey,
                        Enabled = pos.Enabled,
                        DefaultQty = pos.DefaultQty,
                        Name = pos.Name,
                        Unit = pos.Unit,
                        Price = pos.Price,
                        IsCustom = pos.IsCustom
                    };
                    vm.StorageBox.Add(positionCopy);
                    vm.SelectedGroup.Positions.Remove(pos);
                }
            }
            _draggedItemKey = null;
        }
    }

    private void StorageListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
    }

    private void StorageListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && DataContext is PositionTemplateEditorViewModel vm)
        {
            var mousePos = e.GetPosition(null);
            var diff = _startPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (vm.SelectedStoragePosition != null)
                {
                    _draggedItemKey = vm.SelectedStoragePosition.ItemKey;
                    var data = new DataObject("PositionTemplateKey", _draggedItemKey);
                    DragDrop.DoDragDrop(StorageListBox, data, DragDropEffects.Move);
                }
            }
        }
    }

    private void PositionsDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("PositionTemplateKey") && DataContext is PositionTemplateEditorViewModel vm)
        {
            var key = e.Data.GetData("PositionTemplateKey") as string;
            if (key != null && vm.SelectedGroup != null)
            {
                var pos = vm.StorageBox.FirstOrDefault(p => p.ItemKey == key);
                if (pos != null)
                {
                    var positionCopy = new PositionTemplate
                    {
                        ItemKey = pos.ItemKey,
                        Enabled = pos.Enabled,
                        DefaultQty = pos.DefaultQty,
                        Name = pos.Name,
                        Unit = pos.Unit,
                        Price = pos.Price,
                        IsCustom = pos.IsCustom
                    };
                    vm.SelectedGroup.Positions.Add(positionCopy);
                    vm.StorageBox.Remove(pos);
                }
            }
            _draggedItemKey = null;
        }
    }

    private void StorageListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void PositionsDataGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }
}