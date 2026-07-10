using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingApplyProtocolUpdateBuilderTests
{
    [Fact]
    public void Create_builds_new_document_for_record_without_protocol()
    {
        var record = new HaltungRecord();
        record.Fields["Haltungsname"] = "H-100";

        var update = CodingApplyProtocolUpdateBuilder.Create(
            record,
            [
                Event("BAA"),
                Event(" ")
            ]);

        Assert.Equal("H-100", update.Document.HaltungId);
        Assert.Same(update.Document.Current, update.CurrentRevision);
        Assert.Empty(update.CurrentRevision.Entries);
        Assert.Equal(1, update.EventEntryCount);
    }

    [Fact]
    public void Create_clones_existing_protocol_before_apply()
    {
        var record = new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                HaltungId = "EXISTING",
                Current = new ProtocolRevision
                {
                    Entries =
                    {
                        new ProtocolEntry { Code = "OLD", Beschreibung = "Original" }
                    }
                }
            }
        };

        var update = CodingApplyProtocolUpdateBuilder.Create(record, [Event("BAB")]);

        Assert.NotSame(record.Protocol, update.Document);
        Assert.NotSame(record.Protocol.Current, update.CurrentRevision);
        Assert.NotSame(record.Protocol.Current.Entries[0], update.CurrentRevision.Entries[0]);
        Assert.Equal("OLD", update.CurrentRevision.Entries[0].Code);
        Assert.Equal(1, update.EventEntryCount);

        update.CurrentRevision.Entries[0].Code = "CHANGED";

        Assert.Equal("OLD", record.Protocol.Current.Entries[0].Code);
    }

    [Fact]
    public void Create_keeps_only_manual_and_explicitly_accepted_events()
    {
        var record = new HaltungRecord();
        var manual = Event("BAA");
        var accepted = AiEvent("BAB", CodingUserDecision.Accepted);
        var edited = AiEvent("BAC", CodingUserDecision.AcceptedWithEdit);
        var pending = AiEvent("BAD", CodingUserDecision.Ignored);
        var rejected = AiEvent("BAE", CodingUserDecision.Rejected);

        var update = CodingApplyProtocolUpdateBuilder.Create(
            record,
            [manual, accepted, edited, pending, rejected]);

        Assert.Equal(3, update.EventEntryCount);
        Assert.Equal([manual, accepted, edited], update.Events);
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };

    private static CodingEvent AiEvent(string code, CodingUserDecision decision)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = code,
                Confidence = 0.99,
                Decision = decision
            }
        };
}
