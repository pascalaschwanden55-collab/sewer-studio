using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingImportReferenceStateResetter
{
    public static int ClearEvents(ObservableCollection<CodingEvent> importEvents)
    {
        ArgumentNullException.ThrowIfNull(importEvents);

        var removed = importEvents.Count;
        importEvents.Clear();
        return removed;
    }
}
