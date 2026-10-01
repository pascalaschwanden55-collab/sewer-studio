using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Haelt die Formularfelder eines Datensatzes mit dem Datensatz gleich, solange Tabelle und
/// Formular gemeinsam sichtbar sind (Nova-Etappe 1, Nachpruefung W01; Nova-Etappe 2, Fix-Runde 1:
/// dieselbe Regel fuer Schaechte). Jede Feldaenderung am Datensatz (Tabelle, QGIS, Kataster,
/// Umbenennung, Protokoll-Neuaufbau) landet ohne Rueckschreiben im passenden
/// <see cref="RecordDetailItem"/>. Ein gerade bearbeitetes Feld wird nicht unter dem Cursor
/// ersetzt; dort greift der Konfliktschutz im Rueckschreibweg der Fabrik.
///
/// Der Haltungs-Konstruktor bleibt als duenne Fassade erhalten; die allgemeine Ueberladung
/// nimmt jede <see cref="INotifyPropertyChanged"/>-Quelle mit demselben
/// <c>Fields</c>/<c>Fields[Name]</c>-Meldemuster (HaltungRecord UND SchachtRecord melden beide
/// genau so).
/// </summary>
public sealed class DataPageDetailLiveSync : IDisposable
{
    private const string FeldPraefix = "Fields[";
    private const string FeldSammelName = "Fields";
    private readonly INotifyPropertyChanged _source;
    private readonly Func<string, string?> _wert;
    private readonly Dictionary<string, RecordDetailItem> _items;
    private bool _disposed;

    public DataPageDetailLiveSync(HaltungRecord record, IEnumerable<RecordDetailGroup> groups)
        : this(record, record.GetFieldValue, groups)
    {
    }

    /// <summary>
    /// Allgemeine Ueberladung fuer jeden Datensatz mit demselben Meldemuster (aktuell
    /// <see cref="HaltungRecord"/> und <see cref="SchachtRecord"/>). <paramref name="wert"/>
    /// liefert den aktuellen Feldwert des Datensatzes.
    /// </summary>
    public DataPageDetailLiveSync(
        INotifyPropertyChanged source,
        Func<string, string?> wert,
        IEnumerable<RecordDetailGroup> groups)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _wert = wert ?? throw new ArgumentNullException(nameof(wert));
        ArgumentNullException.ThrowIfNull(groups);
        _items = groups
            .SelectMany(g => g.Items)
            .Where(i => !string.IsNullOrEmpty(i.FieldName))
            .GroupBy(i => i.FieldName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _source.PropertyChanged += OnRecordChanged;
    }

    /// <summary>Anzahl der angebundenen Felder (fuer Tests und Diagnose).</summary>
    public int FeldAnzahl => _items.Count;

    private void OnRecordChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed)
            return;

        var name = e.PropertyName ?? string.Empty;
        if (name == FeldSammelName)
        {
            // Sammelmeldung (RaiseAllFieldsChanged oder erster Teil jeder Feldaenderung).
            foreach (var (feld, item) in _items)
                item.UebernehmeAusDatensatz(_wert(feld) ?? "");
            return;
        }

        if (name.StartsWith(FeldPraefix, StringComparison.Ordinal) && name.EndsWith(']'))
        {
            var feld = name[FeldPraefix.Length..^1];
            if (_items.TryGetValue(feld, out var item))
                item.UebernehmeAusDatensatz(_wert(feld) ?? "");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _source.PropertyChanged -= OnRecordChanged;
    }
}
