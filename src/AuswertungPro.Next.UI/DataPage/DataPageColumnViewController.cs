using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Blendet Spalten nach einer gespeicherten Ansicht ein oder aus. Die persoenliche
/// Spaltenanordnung (DataPageGridLayoutController) bleibt bestehen; „Alle Spalten" zeigt
/// wieder jede Feldspalte. Technische Spalten ohne Feldname werden nie angefasst.
/// </summary>
public sealed class DataPageColumnViewController
{
    private readonly DataGrid _grid;
    private readonly Func<DataGridColumn, string?> _fieldNameOf;
    private readonly Action<string> _store;

    public DataPageColumnViewController(
        DataGrid grid,
        Func<DataGridColumn, string?> fieldNameOf,
        Func<string?> getStored,
        Action<string> store)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _fieldNameOf = fieldNameOf ?? throw new ArgumentNullException(nameof(fieldNameOf));
        ArgumentNullException.ThrowIfNull(getStored);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ActiveKey = DataPageColumnViewCatalog.Resolve(getStored()).Key;
    }

    public string ActiveKey { get; private set; }

    public void Apply(string? key)
    {
        var view = DataPageColumnViewCatalog.Resolve(key);
        foreach (var column in _grid.Columns)
        {
            var field = _fieldNameOf(column);
            if (field is null)
                continue;

            var sichtbar = view.Felder is null || view.Felder.Contains(field, StringComparer.Ordinal);
            column.Visibility = sichtbar ? Visibility.Visible : Visibility.Collapsed;
        }

        ActiveKey = view.Key;
        _store(view.Key);
    }
}
