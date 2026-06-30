using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DataPage
{
    private void Grid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.TrackSelectedCells();
    }

    private void Grid_CurrentCellChanged(object sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.TrackCurrentCell();
    }

    private void Grid_ColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        if (e.OriginalSource is not DependencyObject dep)
            return;

        _columnAlignmentToolbar.TrackHeaderClick(dep);
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
        _columnAlignmentToolbar.ApplyHorizontalAlignment(HorizontalAlignment.Left);
    }

    private void AlignCenterButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyHorizontalAlignment(HorizontalAlignment.Center);
    }

    private void AlignRightButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyHorizontalAlignment(HorizontalAlignment.Right);
    }

    private void AlignTopButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyVerticalAlignment(VerticalAlignment.Top);
    }

    private void AlignMiddleButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyVerticalAlignment(VerticalAlignment.Center);
    }

    private void AlignBottomButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyVerticalAlignment(VerticalAlignment.Bottom);
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
