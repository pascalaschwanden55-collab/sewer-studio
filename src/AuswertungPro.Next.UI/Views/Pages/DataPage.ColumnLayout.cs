using System;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.UI.DataPage;

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

    /// <summary>Laden und Speichern liegen in <see cref="DataPageLayoutPersistenz"/>.</summary>
    private void RestoreLayoutFromSettings()
        => DataPageLayoutPersistenz.Restore(Grid, _columnLayoutController, SettingsOderNull);

    private void SaveLayoutToSettings()
        => DataPageLayoutPersistenz.Save(Grid, _columnLayoutController, SettingsOderNull);

    /// <summary>Die Einstellungen — oder null, solange die Seite kein ViewModel hat.</summary>
    private AppSettings? SettingsOderNull
        => DataContext is AuswertungPro.Next.UI.ViewModels.Pages.DataPageViewModel ? Settings : null;

    private void QueueLayoutSave()
    {
        if (_columnLayoutController.IsRestoring)
            return;

        _layoutSaveDebounceTimer.Stop();
        _layoutSaveDebounceTimer.Start();
    }
}
