using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolEventCollectionAppenderTests
{
    [Fact]
    public void Append_adds_events_to_target_in_source_order()
    {
        var existing = Event("OLD");
        var first = Event("BAB");
        var second = Event("BAJ");
        var target = new ObservableCollection<CodingEvent> { existing };

        var added = CodingProtocolEventCollectionAppender.Append(target, [first, second]);

        Assert.Equal(2, added);
        Assert.Equal([existing, first, second], target.ToArray());
    }

    [Fact]
    public void Append_handles_empty_source_without_changing_target()
    {
        var existing = Event("OLD");
        var target = new ObservableCollection<CodingEvent> { existing };

        var added = CodingProtocolEventCollectionAppender.Append(target, []);

        Assert.Equal(0, added);
        Assert.Equal([existing], target.ToArray());
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
