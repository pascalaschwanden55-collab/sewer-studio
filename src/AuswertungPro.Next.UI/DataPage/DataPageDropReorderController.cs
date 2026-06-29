using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageDropReorderController
{
    public static bool TryMoveAndRenumber(
        ObservableCollection<HaltungRecord> records,
        HaltungRecord droppedRecord,
        HaltungRecord targetRecord)
    {
        if (ReferenceEquals(droppedRecord, targetRecord))
            return false;

        var oldIndex = records.IndexOf(droppedRecord);
        var newIndex = records.IndexOf(targetRecord);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
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
