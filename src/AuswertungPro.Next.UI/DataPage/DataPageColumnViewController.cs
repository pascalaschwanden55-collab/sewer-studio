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
    private readonly Func<string?, DataPageColumnView> _resolve;
    private readonly Func<string, string>? _falte;

    /// <param name="falte">
    /// Nova-Fixwelle F2: Namensvergleich der Liste. Schachtfelder heissen nach der Kopfzeile der
    /// Excel-Vorlage ("Eigentümer" mit Umlaut), waehrend Import und Katalog "Eigentuemer" schreiben.
    /// Ein reiner Ordinalvergleich findet die Spalte deshalb nicht und blendet sie still aus.
    /// Ohne Angabe gilt der bisherige Ordinalvergleich.
    /// </param>
    public DataPageColumnViewController(
        DataGrid grid,
        Func<DataGridColumn, string?> fieldNameOf,
        Func<string?> getStored,
        Action<string> store,
        Func<string?, DataPageColumnView>? resolve = null,
        Func<string, string>? falte = null)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _fieldNameOf = fieldNameOf ?? throw new ArgumentNullException(nameof(fieldNameOf));
        ArgumentNullException.ThrowIfNull(getStored);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resolve = resolve ?? DataPageColumnViewCatalog.Resolve;
        _falte = falte;
        ActiveKey = _resolve(getStored()).Key;
    }

    public string ActiveKey { get; private set; }

    /// <summary>Der Namensvergleich dieser Liste; der Chip-Zaehler verwendet denselben.</summary>
    public Func<string, string>? Falte => _falte;

    public void Apply(string? key)
    {
        var view = _resolve(key);
        foreach (var column in _grid.Columns)
        {
            var field = _fieldNameOf(column);
            if (field is null)
                continue;

            column.Visibility = view.Enthaelt(field, _falte) ? Visibility.Visible : Visibility.Collapsed;
        }

        ActiveKey = view.Key;
        _store(view.Key);
    }
}
