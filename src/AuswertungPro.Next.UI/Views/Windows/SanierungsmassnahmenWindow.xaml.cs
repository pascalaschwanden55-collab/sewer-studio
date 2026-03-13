using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class SanierungsmassnahmenWindow : Window
{
    private Point _dragStartPoint;
    private CatalogItemOption? _draggedItem;
    private bool _isDragging;

    public SanierungsmassnahmenWindow(SanierungsmassnahmenViewModel vm)
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        DataContext = vm;

        vm.CostCalcVm.Saved += () => Close();
        vm.CloseRequested += () => Close();
        Closed += (_, _) =>
        {
            vm.Dispose();
            vm.CostCalcVm.Dispose();
        };
        Loaded += (_, _) =>
        {
            EnsureVisibleOnScreen();
            ApplyInitialSelection(vm.CostCalcVm);

            if (vm.InitialFocus == InitialFocusMode.AiOptimization
                && vm.OptimizationVm is not null
                && vm.OptimizationVm.OptimizeCommand.CanExecute(null))
            {
                vm.OptimizationVm.OptimizeCommand.Execute(null);
            }
        };
    }

    // ── Measure list selection ────────────────────────────

    private void MeasuresList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SanierungsmassnahmenViewModel vm)
            return;
        if (sender is not ListBox list)
            return;

        vm.CostCalcVm.SetSelectedMeasures(list.SelectedItems.Cast<MeasureTemplateListItem>());
    }

    private void ApplyInitialSelection(CostCalculatorViewModel costVm)
    {
        if (costVm.InitialMeasureIds.Count == 0)
            return;

        foreach (var item in MeasuresList.Items)
        {
            if (item is MeasureTemplateListItem tpl &&
                !tpl.Disabled &&
                costVm.InitialMeasureIds.Contains(tpl.Id, StringComparer.OrdinalIgnoreCase))
            {
                MeasuresList.SelectedItems.Add(tpl);
            }
        }

        costVm.SetSelectedMeasures(MeasuresList.SelectedItems.Cast<MeasureTemplateListItem>());
    }

    private void MeasuresList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not SanierungsmassnahmenViewModel vm)
            return;

        if (vm.CostCalcVm.ApplyMeasuresCommand.CanExecute(null))
            vm.CostCalcVm.ApplyMeasuresCommand.Execute(null);
    }

    // ── Drag & Drop: Catalog → Measure Block ──────────────

    private void CatalogList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
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

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
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

    // ── Window-level Drop handling ──────────────────────

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("CatalogItemKey"))
            return;

        if (DataContext is SanierungsmassnahmenViewModel vm && vm.CostCalcVm.SelectedMeasures.Count > 0)
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

        var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));
        var block = FindMeasureBlock(hit?.VisualHit);

        if (block is null && DataContext is SanierungsmassnahmenViewModel vm && vm.CostCalcVm.SelectedMeasures.Count > 0)
            block = vm.CostCalcVm.SelectedMeasures[0];

        if (block is null)
            return;

        block.AddLineFromCatalogKey(key);
        e.Handled = true;
    }

    // ── Warning click → navigate to position ──────────────

    private void WarningItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ConsistencyWarning warning)
            return;
        if (DataContext is not SanierungsmassnahmenViewModel vm)
            return;

        var blocks = vm.CostCalcVm.SelectedMeasures;
        MeasureBlockVm? targetBlock = null;
        CostLineVm? targetLine = null;

        // 1) Find block by MeasureId
        if (!string.IsNullOrWhiteSpace(warning.MeasureId))
        {
            targetBlock = blocks.FirstOrDefault(b =>
                string.Equals(b.MeasureId, warning.MeasureId, StringComparison.OrdinalIgnoreCase));
        }

        // 2) Find line by ItemKey within block
        if (targetBlock is not null && !string.IsNullOrWhiteSpace(warning.ItemKey))
        {
            targetLine = targetBlock.Lines.FirstOrDefault(l =>
                string.Equals(l.ItemKey, warning.ItemKey, StringComparison.OrdinalIgnoreCase));
        }

        // 3) Fallback: search all blocks by ItemKey (e.g. KK10 cross-Haltung)
        if (targetBlock is null && !string.IsNullOrWhiteSpace(warning.ItemKey))
        {
            foreach (var b in blocks)
            {
                var line = b.Lines.FirstOrDefault(l =>
                    string.Equals(l.ItemKey, warning.ItemKey, StringComparison.OrdinalIgnoreCase));
                if (line is not null)
                {
                    targetBlock = b;
                    targetLine = line;
                    break;
                }
            }
        }

        if (targetBlock is null)
            return;

        var blockIndex = blocks.IndexOf(targetBlock);
        if (blockIndex < 0)
            return;

        // Direct navigation — no dispatcher delay
        try
        {
            NavigateToBlock(blockIndex, targetLine);
        }
        catch
        {
            // Swallow layout exceptions
        }
    }

    private void NavigateToBlock(int blockIndex, CostLineVm? targetLine)
    {
        // Ensure visual tree is up to date
        CalcItemsControl.UpdateLayout();

        var container = CalcItemsControl.ItemContainerGenerator.ContainerFromIndex(blockIndex) as FrameworkElement;
        if (container is null)
            return;

        // Scroll the block into view and update layout so children are available
        container.BringIntoView();
        CalcScrollViewer.UpdateLayout();

        // Flash the block border (2s bright cyan)
        var blockBorder = FindDescendantWithBorderThickness(container);
        if (blockBorder is not null)
        {
            var origBrush = blockBorder.BorderBrush;
            var origThickness = blockBorder.BorderThickness;
            blockBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xDD, 0xFF));
            blockBorder.BorderThickness = new Thickness(3);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                blockBorder.BorderBrush = origBrush;
                blockBorder.BorderThickness = origThickness;
                timer.Stop();
            };
            timer.Start();
        }

        if (targetLine is null)
            return;

        // Find DataGrid, select + scroll to the row
        var dataGrid = FindDescendant<DataGrid>(container);
        if (dataGrid is null)
            return;

        dataGrid.UpdateLayout();
        dataGrid.SelectedItem = targetLine;
        dataGrid.ScrollIntoView(targetLine);
        dataGrid.UpdateLayout();

        // Flash the row bright blue
        var row = dataGrid.ItemContainerGenerator.ContainerFromItem(targetLine) as DataGridRow;
        if (row is null)
            return;

        var origBg = row.Background;
        row.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x25, 0x63, 0xEB));

        var flashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        flashTimer.Tick += (_, _) =>
        {
            row.Background = origBg;
            flashTimer.Stop();
        };
        flashTimer.Start();
    }

    /// <summary>Finds the first Border that has a non-zero BorderThickness (the DataTemplate block border).</summary>
    private static Border? FindDescendantWithBorderThickness(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Border b && b.BorderThickness.Left > 0)
                return b;
            var result = FindDescendantWithBorderThickness(child);
            if (result is not null)
                return result;
        }
        return null;
    }

    private void SuppressWarning_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        // MenuItem → ContextMenu → PlacementTarget (the Button)
        if (menuItem.Parent is not ContextMenu ctx) return;
        if (ctx.PlacementTarget is not FrameworkElement fe) return;
        if (fe.DataContext is not ConsistencyWarning warning) return;
        if (DataContext is not SanierungsmassnahmenViewModel vm) return;

        vm.CostCalcVm.SuppressWarning(warning);
    }

    private void ResetSuppressedWarnings_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not SanierungsmassnahmenViewModel vm) return;
        vm.CostCalcVm.ResetSuppressedWarnings();
    }

    /// <summary>Depth-first search for a descendant of given type.</summary>
    private static T? FindDescendant<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null) return null;
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindDescendant<T>(child);
            if (result is not null) return result;
        }
        return null;
    }

    // ── Helpers ──────────────────────────────────────────

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj is not null)
        {
            if (obj is T target) return target;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
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

    private void EnsureVisibleOnScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width) Width = area.Width - 20;
        if (Height > area.Height) Height = area.Height - 20;
        if (Left < area.Left) Left = area.Left;
        if (Top < area.Top) Top = area.Top;
        if (Left + Width > area.Right) Left = area.Right - Width;
        if (Top + Height > area.Bottom) Top = area.Bottom - Height;
    }
}
