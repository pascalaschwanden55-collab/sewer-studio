using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class CostCalculatorWindow : Window
{
    private Point _dragStartPoint;
    private CatalogItemOption? _draggedItem;
    private bool _isDragging;

    public CostCalculatorWindow(CostCalculatorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.Saved += () => Close();
        Loaded += (_, __) => ApplyInitialSelection(vm);
    }

    private void MeasuresList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not CostCalculatorViewModel vm)
            return;
        if (sender is not ListBox list)
            return;

        vm.SetSelectedMeasures(list.SelectedItems.Cast<MeasureTemplateListItem>());
    }

    private void ApplyInitialSelection(CostCalculatorViewModel vm)
    {
        if (vm.InitialMeasureIds.Count == 0)
            return;

        foreach (var item in MeasuresList.Items)
        {
            if (item is MeasureTemplateListItem tpl &&
                !tpl.Disabled &&
                vm.InitialMeasureIds.Contains(tpl.Id, System.StringComparer.OrdinalIgnoreCase))
            {
                MeasuresList.SelectedItems.Add(tpl);
            }
        }

        vm.SetSelectedMeasures(MeasuresList.SelectedItems.Cast<MeasureTemplateListItem>());
    }

    private void MeasuresList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CostCalculatorViewModel vm)
            return;

        if (vm.ApplyMeasuresCommand.CanExecute(null))
            vm.ApplyMeasuresCommand.Execute(null);
    }

    // ── Drag & Drop: Catalog → Measure Block ──────────────────────

    private void CatalogList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;

        // Resolve the item under the cursor NOW (before selection changes)
        _draggedItem = null;
        if (sender is ListBox listBox)
        {
            var hit = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            var item = FindAncestor<ListBoxItem>(hit?.VisualHit);
            if (item?.DataContext is CatalogItemOption opt)
                _draggedItem = opt;
        }
    }

    private void CatalogList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        if (_draggedItem is null)
            return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;

        if (System.Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            System.Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (_isDragging)
            return;

        if (sender is not ListBox listBox)
            return;

        _isDragging = true;
        var data = new DataObject("CatalogItemKey", _draggedItem.Key);
        DragDrop.DoDragDrop(listBox, data, DragDropEffects.Copy);
        _isDragging = false;
        _draggedItem = null;
    }

    private void LinesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.DataContext is not CostLineVm line)
            return;

        line.TransferMarked = !line.TransferMarked;
        row.IsSelected = true;
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj is not null)
        {
            if (obj is T target) return target;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    // ── Window-level Drop handling ──────────────────────

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("CatalogItemKey"))
            return;

        // Allow drop anywhere when there are measures loaded
        if (DataContext is CostCalculatorViewModel vm && vm.SelectedMeasures.Count > 0)
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("CatalogItemKey"))
            return;

        var key = e.Data.GetData("CatalogItemKey") as string;
        if (string.IsNullOrWhiteSpace(key))
            return;

        // Try hit-test first to find specific block under cursor
        var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));
        var block = FindMeasureBlock(hit?.VisualHit);

        // Fallback: use first (or only) selected measure
        if (block is null && DataContext is CostCalculatorViewModel vm && vm.SelectedMeasures.Count > 0)
            block = vm.SelectedMeasures[0];

        if (block is null)
            return;

        block.AddLineFromCatalogKey(key);
        e.Handled = true;
    }

    private static MeasureBlockVm? FindMeasureBlock(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement fe && fe.DataContext is MeasureBlockVm vm)
                return vm;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
