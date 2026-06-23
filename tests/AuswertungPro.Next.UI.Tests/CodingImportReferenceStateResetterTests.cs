using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportReferenceStateResetterTests
{
    [Fact]
    public void ClearEvents_clears_import_reference_events_and_returns_removed_count()
    {
        var importEvents = new ObservableCollection<CodingEvent>
        {
            new(),
            new()
        };

        var removed = CodingImportReferenceStateResetter.ClearEvents(importEvents);

        Assert.Equal(2, removed);
        Assert.Empty(importEvents);
    }

    [Fact]
    public void ClearEvents_handles_empty_import_reference_events()
    {
        var importEvents = new ObservableCollection<CodingEvent>();

        var removed = CodingImportReferenceStateResetter.ClearEvents(importEvents);

        Assert.Equal(0, removed);
        Assert.Empty(importEvents);
    }
}
