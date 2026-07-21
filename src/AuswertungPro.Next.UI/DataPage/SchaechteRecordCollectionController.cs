using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

internal sealed class SchaechteRecordCollectionController
{
    private readonly Func<ObservableCollection<SchachtRecord>> _getRecords;
    private readonly Func<IEnumerable<string>> _getColumns;
    private readonly object _collectionLock;

    internal SchaechteRecordCollectionController(
        Func<ObservableCollection<SchachtRecord>> getRecords,
        Func<IEnumerable<string>> getColumns,
        object collectionLock)
    {
        _getRecords = getRecords ?? throw new ArgumentNullException(nameof(getRecords));
        _getColumns = getColumns ?? throw new ArgumentNullException(nameof(getColumns));
        _collectionLock = collectionLock ?? throw new ArgumentNullException(nameof(collectionLock));
    }

    internal SchachtRecord Add()
    {
        var records = _getRecords();
        var columns = _getColumns().ToArray();
        var record = new SchachtRecord();
        foreach (var column in columns)
            record.Fields[column] = "";

        lock (_collectionLock)
        {
            var nrColumn = SchaechteFieldLogic.ResolveNrColumnName(columns, records);
            if (!string.IsNullOrWhiteSpace(nrColumn))
                record.Fields[nrColumn] = (records.Count + 1).ToString();

            records.Add(record);
        }

        return record;
    }

    internal bool TryRemove(SchachtRecord? selected, out SchachtRecord? nextSelection)
    {
        nextSelection = null;
        if (selected is null)
            return false;

        var records = _getRecords();
        lock (_collectionLock)
        {
            var index = records.IndexOf(selected);
            if (index < 0)
                return false;

            records.RemoveAt(index);
            nextSelection = index < records.Count ? records[index] : records.LastOrDefault();
        }

        return true;
    }

    internal bool CanMoveUp(SchachtRecord? selected)
        => GetIndex(selected) > 0;

    internal bool CanMoveDown(SchachtRecord? selected)
    {
        if (selected is null)
            return false;

        var records = _getRecords();
        lock (_collectionLock)
        {
            var index = records.IndexOf(selected);
            return index >= 0 && index < records.Count - 1;
        }
    }

    internal bool TryMoveUp(SchachtRecord? selected)
    {
        if (selected is null)
            return false;

        var records = _getRecords();
        lock (_collectionLock)
        {
            var index = records.IndexOf(selected);
            if (index <= 0)
                return false;

            records.Move(index, index - 1);
        }

        return true;
    }

    internal bool TryMoveDown(SchachtRecord? selected)
    {
        if (selected is null)
            return false;

        var records = _getRecords();
        lock (_collectionLock)
        {
            var index = records.IndexOf(selected);
            if (index < 0 || index >= records.Count - 1)
                return false;

            records.Move(index, index + 1);
        }

        return true;
    }

    internal bool TryMoveToPosition(SchachtRecord? selected, int targetPosition)
    {
        if (selected is null)
            return false;

        var records = _getRecords();
        lock (_collectionLock)
        {
            var oldIndex = records.IndexOf(selected);
            if (!RecordMovePositionCalculator.TryResolveTargetIndex(
                    oldIndex,
                    records.Count,
                    targetPosition,
                    out var targetIndex))
            {
                return false;
            }

            records.Move(oldIndex, targetIndex);
        }

        return true;
    }

    internal void Renumber()
    {
        var records = _getRecords();
        var columns = _getColumns().ToArray();
        lock (_collectionLock)
        {
            var nrField = SchaechteFieldLogic.ResolveNrColumnName(columns, records);
            if (string.IsNullOrWhiteSpace(nrField))
                return;

            for (var index = 0; index < records.Count; index++)
                records[index].SetFieldValue(nrField, (index + 1).ToString());
        }
    }

    private int GetIndex(SchachtRecord? selected)
    {
        if (selected is null)
            return -1;

        var records = _getRecords();
        lock (_collectionLock)
            return records.IndexOf(selected);
    }
}
