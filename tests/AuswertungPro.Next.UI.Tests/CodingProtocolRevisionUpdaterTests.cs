using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

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

    [Fact]
    public void ApplyCodingEvents_does_not_apply_pending_or_rejected_ai_events()
    {
        var revision = new ProtocolRevision();
        var accepted = Event(Entry(Guid.NewGuid(), "BAA", "akzeptiert"));
        accepted.AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Accepted };
        var pending = Event(Entry(Guid.NewGuid(), "BAB", "offen"));
        pending.AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Ignored };
        var rejected = Event(Entry(Guid.NewGuid(), "BAC", "abgelehnt"));
        rejected.AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Rejected };

        var count = CodingProtocolRevisionUpdater.ApplyCodingEvents(
            revision,
            [accepted, pending, rejected]);

        Assert.Equal(1, count);
        Assert.Single(revision.Entries);
        Assert.Equal("BAA", revision.Entries[0].Code);
    }

    /// <summary>
    /// WAECHTER gegen einen bereits einmal gebauten und wieder verworfenen Fix.
    ///
    /// Die selbst ergaenzten Rohrgrenzen tragen das Kennzeichen "auto_boundary".
    /// Es liegt nahe, sie damit vor dem Loeschen zu schuetzen - dann ueberlebten
    /// sie ein zweites "Uebernehmen". Das ist FALSCH: Beim erneuten Oeffnen des
    /// Codiermodus laedt CodingSessionService.LoadExistingObservations jeden
    /// Protokolleintrag als Ereignis, auch BCD und BCE. Loescht der Mensch dort ein
    /// falsches Rohrende, ist diese Loeschung von "war nie ein Ereignis" nicht mehr
    /// zu unterscheiden - ein solcher Schutz macht ein falsches Rohrende ueber den
    /// Codiermodus unloeschbar.
    ///
    /// Wird dieser Test rot, wurde der Schutz erneut eingebaut. Die richtige
    /// Loesung waere, die Grenzen als echte Codier-Ereignisse anzulegen.
    /// </summary>
    [Fact]
    public void Ein_geloeschtes_Rohrende_bleibt_geloescht_auch_mit_Auto_Kennzeichen()
    {
        var rohrende = Entry(Guid.NewGuid(), "BCE", "Rohrende");
        rohrende.Ai = new ProtocolEntryAiMeta
        {
            Flags = new List<string> { "foto_required", ProtocolBoundaryService.AutoBoundaryFlag }
        };
        var riss = Entry(Guid.NewGuid(), "BAB", "Riss");
        var revision = new ProtocolRevision { Entries = { riss, rohrende } };

        // Der Mensch hat das Rohrende in der Codierliste geloescht: Es fehlt.
        CodingProtocolRevisionUpdater.ApplyCodingEvents(revision, [Event(riss)]);

        Assert.True(revision.Entries.Single(e => e.Code == "BCE").IsDeleted);
        Assert.False(revision.Entries.Single(e => e.Code == "BAB").IsDeleted);
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
