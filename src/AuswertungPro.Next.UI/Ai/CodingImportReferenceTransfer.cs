using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingImportReferenceTransfer
{
    public static int MoveExistingEventsToImportReference(
        ObservableCollection<CodingEvent> source,
        ObservableCollection<CodingEvent> target)
    {
        var ordered = source
            .OrderBy(e => e.MeterAtCapture)
            .ToList();

        source.Clear();
        target.Clear();

        foreach (var ev in ordered)
            target.Add(ev);

        return ordered.Count;
    }
}
