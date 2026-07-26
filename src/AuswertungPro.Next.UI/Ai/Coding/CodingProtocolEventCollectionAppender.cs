using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolEventCollectionAppender
{
    public static int Append(
        ObservableCollection<CodingEvent> target,
        IEnumerable<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        var count = 0;
        foreach (var codingEvent in events)
        {
            target.Add(codingEvent);
            count++;
        }

        return count;
    }
}
