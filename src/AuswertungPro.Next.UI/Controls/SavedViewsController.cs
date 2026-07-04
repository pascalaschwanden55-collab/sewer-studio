using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Verwaltet benannte Ansichten (Dim4) fuer ein Grid: Filter (ueber
/// <see cref="ISavedViewFilterProvider"/> am DataContext), Spalten (Breite/Reihenfolge/Sichtbarkeit)
/// und Sortierung als ein Paket speichern/laden/loeschen.
/// </summary>
public sealed class SavedViewsController
{
    private readonly DataGrid _grid;
    private readonly string _viewKey;

    public ObservableCollection<string> Names { get; } = new();

    public SavedViewsController(DataGrid grid, string viewKey)
    {
        _grid = grid;
        _viewKey = viewKey;
    }

    public void RefreshNames()
    {
        Names.Clear();
        foreach (var name in SavedViewsStore.Names(_viewKey))
            Names.Add(name);
    }

    /// <summary>Speichert die aktuelle Ansicht unter <paramref name="name"/> (ueberschreibt gleichnamige).</summary>
    public void Save(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        var (sortField, sortDir) = CaptureSort();
        SavedViewsStore.Upsert(_viewKey, new SavedView
        {
            Name = trimmed,
            FilterJson = Provider?.CaptureFilterState(),
            Columns = new DataGridColumnLayoutController().Capture(_grid.Columns),
            SortFieldName = sortField,
            SortDirection = sortDir
        });
        RefreshNames();
    }

    /// <summary>Wendet die gespeicherte Ansicht an (Filter -&gt; Spalten -&gt; Sortierung).</summary>
    public void Apply(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var view = SavedViewsStore.Get(_viewKey, name!);
        if (view is null)
            return;

        Provider?.ApplyFilterState(view.FilterJson);

        if (view.Columns is not null)
            new DataGridColumnLayoutController().Restore(_grid.Columns, view.Columns);

        ApplySort(view.SortFieldName, view.SortDirection);
    }

    public void Delete(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        SavedViewsStore.Delete(_viewKey, name!);
        RefreshNames();
    }

    private ISavedViewFilterProvider? Provider => _grid.DataContext as ISavedViewFilterProvider;

    private (string? Field, string? Direction) CaptureSort()
    {
        if (_grid.Items.SortDescriptions.Count == 0)
            return (null, null);

        var first = _grid.Items.SortDescriptions[0];
        return (first.PropertyName, first.Direction.ToString());
    }

    private void ApplySort(string? field, string? direction)
    {
        _grid.Items.SortDescriptions.Clear();
        foreach (var column in _grid.Columns)
            column.SortDirection = null;

        if (string.IsNullOrWhiteSpace(field))
            return;

        var dir = string.Equals(direction, nameof(ListSortDirection.Descending), System.StringComparison.OrdinalIgnoreCase)
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        _grid.Items.SortDescriptions.Add(new SortDescription(field, dir));

        var sortedColumn = _grid.Columns.FirstOrDefault(c =>
            string.Equals(c.SortMemberPath, field, System.StringComparison.Ordinal));
        if (sortedColumn is not null)
            sortedColumn.SortDirection = dir;
    }
}
