using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolRevisionUpdaterTests
{
    [Fact]
    public void ApplyCodingEvents_updates_existing_entries_and_marks_missing_entries_deleted()
    {
        var keepId = Guid.NewGuid();
        var deleteId = Guid.NewGuid();
        var revision = new ProtocolRevision
        {
            Entries =
            {
                Entry(keepId, "OLD", "old", isDeleted: true),
                Entry(deleteId, "DEL", "delete")
            }
        };
        var updated = Entry(keepId, "NEW", "updated");
        updated.MeterStart = 4.2;

        var count = CodingProtocolRevisionUpdater.ApplyCodingEvents(
            revision,
            new[] { Event(updated) });

        Assert.Equal(1, count);
        Assert.Equal(2, revision.Entries.Count);
        Assert.Equal("NEW", revision.Entries[0].Code);
        Assert.Equal("updated", revision.Entries[0].Beschreibung);
        Assert.Equal(4.2, revision.Entries[0].MeterStart);
        Assert.False(revision.Entries[0].IsDeleted);
        Assert.True(revision.Entries[1].IsDeleted);
    }

    [Fact]
    public void ApplyCodingEvents_adds_new_entries_and_ignores_empty_codes()
    {
        var revision = new ProtocolRevision();
        var add = Entry(Guid.NewGuid(), "BAB", "Riss");
        var ignored = Entry(Guid.NewGuid(), "", "Leer");

        var count = CodingProtocolRevisionUpdater.ApplyCodingEvents(
            revision,
            new[] { Event(add), Event(ignored) });

        Assert.Equal(1, count);
        Assert.Single(revision.Entries);
        Assert.Same(add, revision.Entries[0]);
    }

    [Fact]
    public void ApplyCodingEvents_uses_last_event_when_ids_are_duplicated()
    {
        var id = Guid.NewGuid();
        var revision = new ProtocolRevision
        {
            Entries = { Entry(id, "OLD", "old") }
        };

        var count = CodingProtocolRevisionUpdater.ApplyCodingEvents(
            revision,
            new[]
            {
                Event(Entry(id, "FIRST", "first")),
                Event(Entry(id, "LAST", "last"))
            });

        Assert.Equal(1, count);
        Assert.Single(revision.Entries);
        Assert.Equal("LAST", revision.Entries[0].Code);
        Assert.Equal("last", revision.Entries[0].Beschreibung);
    }

    private static CodingEvent Event(ProtocolEntry entry)
        => new() { Entry = entry };

    private static ProtocolEntry Entry(Guid id, string code, string description, bool isDeleted = false)
        => new()
        {
            EntryId = id,
            Code = code,
            Beschreibung = description,
            IsDeleted = isDeleted
        };
}
