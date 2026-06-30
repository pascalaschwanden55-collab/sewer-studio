using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed class SchaechtePageSubscriptionController
{
    private readonly Action _rebuildColumns;
    private readonly Action _refreshSearch;
    private readonly PropertyChangedEventHandler _recordPropertyChanged;
    private readonly List<SchachtRecord> _subscribedRecords = new();

    private INotifyCollectionChanged? _columns;
    private INotifyCollectionChanged? _records;
    private Func<IEnumerable<SchachtRecord>> _currentRecords = Array.Empty<SchachtRecord>;

    public SchaechtePageSubscriptionController(
        Action rebuildColumns,
        Action refreshSearch,
        PropertyChangedEventHandler recordPropertyChanged)
    {
        _rebuildColumns = rebuildColumns;
        _refreshSearch = refreshSearch;
        _recordPropertyChanged = recordPropertyChanged;
    }

    public void Switch(
        INotifyCollectionChanged? columns,
        INotifyCollectionChanged? records,
        Func<IEnumerable<SchachtRecord>> currentRecords)
    {
        Detach();

        _columns = columns;
        _records = records;
        _currentRecords = currentRecords;

        if (_columns is null || _records is null)
            return;

        _columns.CollectionChanged += ColumnsChanged;
        _records.CollectionChanged += RecordsChanged;
        AttachRecords(_currentRecords());

        _rebuildColumns();
        _refreshSearch();
    }

    public void Detach()
    {
        if (_columns is not null)
            _columns.CollectionChanged -= ColumnsChanged;
        if (_records is not null)
            _records.CollectionChanged -= RecordsChanged;

        DetachRecords(_subscribedRecords);

        _columns = null;
        _records = null;
        _currentRecords = Array.Empty<SchachtRecord>;
    }

    private void ColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _rebuildColumns();
    }

    private void RecordsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;

        if (e.Action == NotifyCollectionChangedAction.Reset ||
            (e.OldItems is null && e.NewItems is null))
        {
            DetachRecords(_subscribedRecords);
            AttachRecords(_currentRecords());
            _refreshSearch();
            return;
        }

        if (e.OldItems is not null)
            DetachRecords(e.OldItems.OfType<SchachtRecord>());

        if (e.NewItems is not null)
            AttachRecords(e.NewItems.OfType<SchachtRecord>());

        _refreshSearch();
    }

    private void AttachRecords(IEnumerable<SchachtRecord> records)
    {
        foreach (var record in records)
        {
            if (_subscribedRecords.Contains(record))
                continue;

            record.PropertyChanged += _recordPropertyChanged;
            _subscribedRecords.Add(record);
        }
    }

    private void DetachRecords(IEnumerable<SchachtRecord> records)
    {
        foreach (var record in records.ToList())
        {
            record.PropertyChanged -= _recordPropertyChanged;
            _subscribedRecords.Remove(record);
        }
    }
}
