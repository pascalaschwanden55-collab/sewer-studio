using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageDropReorderController
{
    public static bool TryMoveAndRenumber(
        ObservableCollection<HaltungRecord> records,
        HaltungRecord droppedRecord,
        HaltungRecord targetRecord)
        => DataPageRecordOrderController.TryMoveAndRenumber(records, droppedRecord, targetRecord);
}
