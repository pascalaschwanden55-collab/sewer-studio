using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportReferenceTransferTests
{
    [Fact]
    public void MoveExistingEventsToImportReference_clears_target_and_moves_source_sorted_by_meter()
    {
        var late = Event("BAJ", 3.0);
        var early = Event("BAB", 1.2);
        var source = new ObservableCollection<CodingEvent> { late, early };
        var oldTargetEvent = Event("OLD", 9.9);
        var target = new ObservableCollection<CodingEvent> { oldTargetEvent };

        var moved = CodingImportReferenceTransfer.MoveExistingEventsToImportReference(source, target);

        Assert.Equal(2, moved);
        Assert.Empty(source);
        Assert.Equal(new[] { early, late }, target.ToArray());
    }

    [Fact]
    public void MoveExistingEventsToImportReference_handles_empty_source()
    {
        var source = new ObservableCollection<CodingEvent>();
        var target = new ObservableCollection<CodingEvent> { Event("OLD", 1.0) };

        var moved = CodingImportReferenceTransfer.MoveExistingEventsToImportReference(source, target);

        Assert.Equal(0, moved);
        Assert.Empty(source);
        Assert.Empty(target);
    }

    [Fact]
    public void MoveExistingEventsToImportReference_preserves_existing_order_for_equal_meter()
    {
        var first = Event("FIRST", 2.0);
        first.VideoTimestamp = TimeSpan.FromSeconds(10);
        var second = Event("SECOND", 2.0);
        second.VideoTimestamp = TimeSpan.Zero;
        var source = new ObservableCollection<CodingEvent> { first, second };
        var target = new ObservableCollection<CodingEvent>();

        CodingImportReferenceTransfer.MoveExistingEventsToImportReference(source, target);

        Assert.Equal(new[] { first, second }, target.ToArray());
    }

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            MeterAtCapture = meter
        };
}
