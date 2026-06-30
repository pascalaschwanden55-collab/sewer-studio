using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageRecordOrderController
{
    public static bool CanMoveByOffset(
        ObservableCollection<HaltungRecord> records,
        HaltungRecord? selectedRecord,
        int offset)
    {
        if (selectedRecord is null || offset == 0)
            return false;

        var oldIndex = records.IndexOf(selectedRecord);
        if (oldIndex < 0)
            return false;

        var newIndex = oldIndex + offset;
        return newIndex >= 0 && newIndex < records.Count;
    }

    public static bool TryMoveByOffset(
        ObservableCollection<HaltungRecord> records,
        HaltungRecord? selectedRecord,
        int offset)
    {
        if (selectedRecord is null || offset == 0)
            return false;

        var oldIndex = records.IndexOf(selectedRecord);
        if (oldIndex < 0)
            return false;

        return TryMoveIndexAndRenumber(records, oldIndex, oldIndex + offset);
    }

    public static bool TryMoveToPosition(
        ObservableCollection<HaltungRecord> records,
        HaltungRecord? selectedRecord,
        int targetPosition)
    {
        if (selectedRecord is null)
            return false;

        var oldIndex = records.IndexOf(selectedRecord);
        if (oldIndex < 0)
            return false;

        var targetIndex = targetPosition - 1;
        if (targetIndex < 0)
            targetIndex = 0;
        if (targetIndex >= records.Count)
            targetIndex = records.Count - 1;

        return TryMoveIndexAndRenumber(records, oldIndex, targetIndex);
    }

    public static bool TryMoveAndRenumber(
        ObservableCollection<HaltungRecord> records,
        HaltungRecord droppedRecord,
        HaltungRecord targetRecord)
    {
        if (ReferenceEquals(droppedRecord, targetRecord))
            return false;

        var oldIndex = records.IndexOf(droppedRecord);
        var newIndex = records.IndexOf(targetRecord);
        return TryMoveIndexAndRenumber(records, oldIndex, newIndex);
    }

    private static bool TryMoveIndexAndRenumber(ObservableCollection<HaltungRecord> records, int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || newIndex >= records.Count || oldIndex == newIndex)
            return false;

        records.Move(oldIndex, newIndex);
        Renumber(records);
        return true;
    }

    private static void Renumber(ObservableCollection<HaltungRecord> records)
    {
        for (var i = 0; i < records.Count; i++)
            records[i].SetFieldValue("NR", (i + 1).ToString(), FieldSource.Manual, userEdited: true);
    }
}
