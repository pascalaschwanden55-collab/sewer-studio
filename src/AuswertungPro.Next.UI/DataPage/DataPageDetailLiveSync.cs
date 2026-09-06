using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Haelt die Formularfelder einer Haltung mit dem Datensatz gleich, solange Tabelle und
/// Formular gemeinsam sichtbar sind (Nova-Etappe 1, Nachpruefung W01). Jede Feldaenderung am
/// Datensatz (Tabelle, QGIS, Kataster, Umbenennung) landet ohne Rueckschreiben im passenden
/// <see cref="RecordDetailItem"/>. Ein gerade bearbeitetes Feld wird nicht unter dem Cursor
/// ersetzt; dort greift der Konfliktschutz im Rueckschreibweg der Fabrik.
/// </summary>
public sealed class DataPageDetailLiveSync : IDisposable
{
    private const string FeldPraefix = "Fields[";
    private readonly HaltungRecord _record;
    private readonly Dictionary<string, RecordDetailItem> _items;
    private bool _disposed;

    public DataPageDetailLiveSync(HaltungRecord record, IEnumerable<RecordDetailGroup> groups)
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
        ArgumentNullException.ThrowIfNull(groups);
        _items = groups
            .SelectMany(g => g.Items)
            .Where(i => !string.IsNullOrEmpty(i.FieldName))
            .GroupBy(i => i.FieldName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _record.PropertyChanged += OnRecordChanged;
    }

    /// <summary>Anzahl der angebundenen Felder (fuer Tests und Diagnose).</summary>
    public int FeldAnzahl => _items.Count;

    private void OnRecordChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed)
            return;

        var name = e.PropertyName ?? string.Empty;
        if (name == nameof(HaltungRecord.Fields))
        {
            // Sammelmeldung (RaiseAllFieldsChanged oder erster Teil jeder Feldaenderung).
            foreach (var (feld, item) in _items)
                item.UebernehmeAusDatensatz(_record.GetFieldValue(feld));
            return;
        }

        if (name.StartsWith(FeldPraefix, StringComparison.Ordinal) && name.EndsWith(']'))
        {
            var feld = name[FeldPraefix.Length..^1];
            if (_items.TryGetValue(feld, out var item))
                item.UebernehmeAusDatensatz(_record.GetFieldValue(feld));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _record.PropertyChanged -= OnRecordChanged;
    }
}
