using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingApplyChangesWorkflowTests
{
    [Fact]
    public void Execute_skips_without_coding_context()
    {
        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: false,
                HaltungRecord: Record(),
                Events: [Event("BAA")],
                ShowOverlay: true),
            ThrowingActions());

        Assert.Equal(CodingApplyChangesWorkflowOutcome.NoCodingContext, result.Outcome);
        Assert.False(result.Applied);
    }

    [Fact]
    public void Execute_skips_without_events()
    {
        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: Record(),
                Events: null,
                ShowOverlay: true),
            ThrowingActions());

        Assert.Equal(CodingApplyChangesWorkflowOutcome.NoEvents, result.Outcome);
        Assert.False(result.Applied);
    }

    [Fact]
    public void Execute_stops_when_empty_protocol_confirmation_is_cancelled()
    {
        var calls = new List<string>();
        var record = Record();
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new ProtocolEntry { EntryId = Guid.NewGuid(), Code = "OLD" }
                }
            }
        };

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: [],
                ShowOverlay: true),
            ThrowingActions(confirmEmptyProtocol: guard =>
            {
                calls.Add($"confirm:{guard.RequiresConfirmation}");
                return false;
            }));

        Assert.Equal(CodingApplyChangesWorkflowOutcome.EmptyProtocolCancelled, result.Outcome);
        Assert.False(result.Applied);
        Assert.Equal(["confirm:True"], calls);
        Assert.False(record.Protocol.Current.Entries[0].IsDeleted);
    }

    [Fact]
    public void Execute_applies_in_window_order()
    {
        var calls = new List<string>();
        var record = Record();
        var events = new List<CodingEvent>
        {
            Event("BAA"),
            Event("BAB")
        };

        ProtocolDocument? assigned = null;
        ProtocolDocument? synced = null;
        string? baseline = null;
        string? overlayMessage = null;
        TimeSpan? overlayDuration = null;

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: events,
                ShowOverlay: true),
            new CodingApplyChangesWorkflowActions(
                ConfirmEmptyProtocol: guard =>
                {
                    calls.Add($"confirm:{guard.RequiresConfirmation}");
                    return true;
                },
                AddAutomaticBoundaryEvent: entry =>
                {
                    events.Add(Event(entry));
                    calls.Add($"boundary:{entry.Code}");
                },
                AssignProtocol: document =>
                {
                    assigned = document;
                    calls.Add($"assign:{document.Current!.Entries.Count}");
                },
                MarkProjectDirty: () => calls.Add("dirty"),
                SyncCodingToPrimaryDamages: document =>
                {
                    synced = document;
                    calls.Add($"sync:{document.Current!.Entries.Count}");
                },
                PersistCodingEventsAsTrainingSamples: persisted => calls.Add($"training:{persisted.Count}"),
                SetBaselineSignature: signature =>
                {
                    baseline = signature;
                    calls.Add("baseline");
                },
                SaveProjectAfterCoding: () => calls.Add("save"),
                ShowOverlay: (message, duration) =>
                {
                    overlayMessage = message;
                    overlayDuration = duration;
                    calls.Add("overlay");
                }));

        Assert.Equal(CodingApplyChangesWorkflowOutcome.Applied, result.Outcome);
        Assert.True(result.Applied);
        Assert.Equal(
            ["confirm:False", "boundary:BCD", "assign:3", "dirty", "sync:3", "dirty", "training:2", "baseline", "save", "overlay"],
            calls);
        Assert.NotNull(assigned);
        Assert.Same(assigned, synced);
        // Der Rohranfang wird still vorne erg\u00e4nzt.
        Assert.Equal(["BCD", "BAA", "BAB"], assigned.Current!.Entries.Select(entry => entry.Code));
        Assert.Equal(CodingEventsSignatureBuilder.Build(events), baseline);
        Assert.Equal("2 Ereignisse in Prim\u00e4re Sch\u00e4den \u00fcbernommen \u00b7 Rohranfang erg\u00e4nzt", overlayMessage);
        Assert.Equal(TimeSpan.FromSeconds(4), overlayDuration);
    }

    [Fact]
    public void Execute_keeps_overlay_hidden_when_disabled()
    {
        var record = Record();

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: [Event("BAA")],
                ShowOverlay: false),
            ThrowingActions(
                confirmEmptyProtocol: _ => true,
                addAutomaticBoundaryEvent: _ => { },
                assignProtocol: _ => { },
                markProjectDirty: () => { },
                syncCodingToPrimaryDamages: _ => { },
                persistCodingEventsAsTrainingSamples: _ => { },
                setBaselineSignature: _ => { },
                saveProjectAfterCoding: () => { }));

        Assert.Equal(CodingApplyChangesWorkflowOutcome.Applied, result.Outcome);
        Assert.True(result.Applied);
    }

    [Fact]
    public void Rohrende_wird_bei_Ja_am_Vorschlagsmeter_gesetzt()
    {
        var record = Record();
        ProtocolDocument? assigned = null;
        double? prompted = null;
        string? overlayMessage = null;

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: [Event("BABBB")],
                ShowOverlay: true,
                HaltungslaengeM: 20.31),
            Actions(
                assignProtocol: document => assigned = document,
                showOverlay: message => overlayMessage = message,
                confirmMissingPipeEnd: prompt =>
                {
                    prompted = prompt.ProposalMeter;
                    return CodingApplyPipeEndDecision.Insert;
                }));

        Assert.True(result.Applied);
        Assert.Equal(20.31, prompted);
        Assert.Equal(["BCD", "BABBB", "BCE"], assigned!.Current!.Entries.Select(e => e.Code));
        Assert.Equal(0.0, assigned.Current.Entries[0].MeterStart);
        Assert.Equal(20.31, assigned.Current.Entries[2].MeterStart);
        Assert.Equal(
            "1 Ereignisse in Prim\u00e4re Sch\u00e4den \u00fcbernommen \u00b7 Rohranfang und Rohrende erg\u00e4nzt",
            overlayMessage);
    }

    [Fact]
    public void Rohrende_bleibt_bei_Nein_weg_und_die_Codierung_wird_trotzdem_uebernommen()
    {
        var record = Record();
        ProtocolDocument? assigned = null;

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: [Event("BABBB")],
                ShowOverlay: false,
                HaltungslaengeM: 20.31),
            Actions(
                assignProtocol: document => assigned = document,
                confirmMissingPipeEnd: _ => CodingApplyPipeEndDecision.Skip));

        Assert.True(result.Applied);
        Assert.Equal(["BCD", "BABBB"], assigned!.Current!.Entries.Select(e => e.Code));
    }

    [Fact]
    public void Abbrechen_beim_Rohrende_schreibt_gar_nichts()
    {
        var record = Record();
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { EntryId = Guid.NewGuid(), Code = "ALT", MeterStart = 3.0 } }
            }
        };

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: [Event("BABBB")],
                ShowOverlay: true,
                HaltungslaengeM: 20.31),
            ThrowingActions(
                confirmEmptyProtocol: _ => true,
                confirmMissingPipeEnd: _ => CodingApplyPipeEndDecision.Cancel));

        Assert.Equal(CodingApplyChangesWorkflowOutcome.PipeEndCancelled, result.Outcome);
        Assert.False(result.Applied);
        // Der bestehende Stand der Haltung bleibt unber\u00fchrt.
        Assert.Single(record.Protocol.Current!.Entries);
        Assert.Equal("ALT", record.Protocol.Current.Entries[0].Code);
        Assert.False(record.Protocol.Current.Entries[0].IsDeleted);
    }

    [Fact]
    public void Ohne_bekannte_Haltungslaenge_gibt_es_keinen_Meter_im_Vorschlag()
    {
        var record = Record();
        double? prompted = 1.0;

        CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: [Event("BABBB")],
                ShowOverlay: false,
                HaltungslaengeM: null),
            Actions(confirmMissingPipeEnd: prompt =>
            {
                prompted = prompt.ProposalMeter;
                return CodingApplyPipeEndDecision.Skip;
            }));

        Assert.Null(prompted);
    }

    [Fact]
    public void Ein_vorhandenes_Rohrende_loest_keine_Rueckfrage_aus()
    {
        var record = Record();
        ProtocolDocument? assigned = null;

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: [Event("BCD"), Event("BABBB"), Event("BCE")],
                ShowOverlay: false,
                HaltungslaengeM: 20.31),
            Actions(
                assignProtocol: document => assigned = document,
                confirmMissingPipeEnd: _ => throw new InvalidOperationException("Keine R\u00fcckfrage erwartet.")));

        Assert.True(result.Applied);
        Assert.Equal(["BCD", "BABBB", "BCE"], assigned!.Current!.Entries.Select(e => e.Code));
    }

    [Fact]
    public void Eine_bewusst_leere_Codierung_bekommt_keinen_erfundenen_Rohranfang()
    {
        var record = Record();
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { EntryId = Guid.NewGuid(), Code = "ALT" } }
            }
        };
        ProtocolDocument? assigned = null;

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: [],
                ShowOverlay: false,
                HaltungslaengeM: 20.31),
            Actions(
                assignProtocol: document => assigned = document,
                confirmMissingPipeEnd: _ => throw new InvalidOperationException("Keine R\u00fcckfrage erwartet.")));

        Assert.True(result.Applied);
        Assert.All(assigned!.Current!.Entries, entry => Assert.True(entry.IsDeleted));
        Assert.DoesNotContain(assigned.Current.Entries, entry => entry.Code == "BCD");
    }

    private static CodingApplyChangesWorkflowActions Actions(
        Action<ProtocolEntry>? addAutomaticBoundaryEvent = null,
        Action<ProtocolDocument>? assignProtocol = null,
        Action<string>? showOverlay = null,
        Func<CodingApplyPipeEndPrompt, CodingApplyPipeEndDecision>? confirmMissingPipeEnd = null)
        => new(
            ConfirmEmptyProtocol: _ => true,
            AddAutomaticBoundaryEvent: addAutomaticBoundaryEvent ?? (_ => { }),
            AssignProtocol: assignProtocol ?? (_ => { }),
            MarkProjectDirty: () => { },
            SyncCodingToPrimaryDamages: _ => { },
            PersistCodingEventsAsTrainingSamples: _ => { },
            SetBaselineSignature: _ => { },
            SaveProjectAfterCoding: () => { },
            ShowOverlay: (message, _) => showOverlay?.Invoke(message),
            ConfirmMissingPipeEnd: confirmMissingPipeEnd);

    /// <summary>
    /// Automatisch ergaenzte Grenzen werden als echte Codier-Ereignisse registriert.
    /// Darum findet der zweite Lauf dieselben EntryIds wieder und erzeugt weder eine
    /// erneute Rueckfrage noch geloeschte Altstaende.
    /// </summary>
    [Fact]
    public void Zweimal_Uebernehmen_belaesst_dieselben_Grenzen_ohne_Rueckfrage_oder_Tombstones()
    {
        var record = Record();
        var ereignis = Event("BABBB");
        ereignis.Entry.MeterStart = 4.82;
        var events = new List<CodingEvent> { ereignis };
        var rueckfragen = 0;

        void Uebernehmen() => CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: events,
                ShowOverlay: false,
                HaltungslaengeM: 20.31),
            ThrowingActions(
                confirmEmptyProtocol: _ => true,
                addAutomaticBoundaryEvent: entry => events.Add(Event(entry)),
                // Genau wie im Player: die uebernommene Kopie wird die neue Wahrheit.
                assignProtocol: doc => record.Protocol = doc,
                markProjectDirty: () => { },
                syncCodingToPrimaryDamages: _ => { },
                persistCodingEventsAsTrainingSamples: _ => { },
                setBaselineSignature: _ => { },
                saveProjectAfterCoding: () => { },
                confirmMissingPipeEnd: _ =>
                {
                    rueckfragen++;
                    return CodingApplyPipeEndDecision.Insert;
                }));

        Uebernehmen();
        Uebernehmen();

        var entries = record.Protocol!.Current!.Entries;

        Assert.Equal(1, entries.Count(e => e.Code == "BCD"));
        Assert.Equal(1, entries.Count(e => e.Code == "BCE"));
        Assert.DoesNotContain(entries, e => e.IsDeleted);
        Assert.Equal(1, rueckfragen);
        Assert.Equal(3, events.Count);
        Assert.All(
            events.Where(e => e.Entry.Code is "BCD" or "BCE"),
            e => Assert.True(e.Entry.Training?.SkipAutomaticPersistence));
    }

    [Fact]
    public void Neu_geladene_Grenzen_werden_weder_ersetzt_noch_erneut_abgefragt()
    {
        var record = Record();
        var start = ProtocolBoundaryService.InsertPipeStart([]);
        var schaden = Event("BABBB");
        schaden.Entry.MeterStart = 4.82;
        var ende = ProtocolBoundaryService.AppendPipeEnd([], 20.31);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision { Entries = { start, schaden.Entry, ende } }
        };

        // LoadExistingObservations baut aus genau diesen aktiven Eintraegen wieder Events.
        var reloadedEvents = record.Protocol.Current.Entries.Select(Event).ToList();

        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: reloadedEvents,
                ShowOverlay: false,
                HaltungslaengeM: 20.31),
            ThrowingActions(
                confirmEmptyProtocol: _ => true,
                addAutomaticBoundaryEvent: _ => throw new InvalidOperationException("Keine neue Grenze erwartet."),
                assignProtocol: doc => record.Protocol = doc,
                markProjectDirty: () => { },
                syncCodingToPrimaryDamages: _ => { },
                persistCodingEventsAsTrainingSamples: _ => { },
                setBaselineSignature: _ => { },
                saveProjectAfterCoding: () => { },
                confirmMissingPipeEnd: _ => throw new InvalidOperationException("Keine Rueckfrage erwartet.")));

        Assert.True(result.Applied);
        Assert.Equal(3, record.Protocol.Current!.Entries.Count);
        Assert.DoesNotContain(record.Protocol.Current.Entries, e => e.IsDeleted);
    }

    [Fact]
    public void Manuell_entferntes_Rohrende_bleibt_nach_Uebernehmen_geloescht()
    {
        var record = Record();
        var schaden = Event("BABBB");
        schaden.Entry.MeterStart = 4.82;
        var events = new List<CodingEvent> { schaden };
        var rueckfragen = 0;

        void Uebernehmen() => CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: events,
                ShowOverlay: false,
                HaltungslaengeM: 20.31),
            ThrowingActions(
                confirmEmptyProtocol: _ => true,
                addAutomaticBoundaryEvent: entry => events.Add(Event(entry)),
                assignProtocol: doc => record.Protocol = doc,
                markProjectDirty: () => { },
                syncCodingToPrimaryDamages: _ => { },
                persistCodingEventsAsTrainingSamples: _ => { },
                setBaselineSignature: _ => { },
                saveProjectAfterCoding: () => { },
                confirmMissingPipeEnd: _ =>
                {
                    rueckfragen++;
                    return rueckfragen == 1
                        ? CodingApplyPipeEndDecision.Insert
                        : CodingApplyPipeEndDecision.Skip;
                }));

        Uebernehmen();
        events.Remove(events.Single(e => e.Entry.Code == "BCE"));
        Uebernehmen();

        var entries = record.Protocol!.Current!.Entries;
        Assert.Equal(2, rueckfragen);
        Assert.True(entries.Single(e => e.Code == "BCE").IsDeleted);
        Assert.DoesNotContain(entries, e => e.Code == "BCE" && !e.IsDeleted);
        Assert.DoesNotContain(events, e => e.Entry.Code == "BCE");
    }

    /// <summary>
    /// Ein vom Menschen im Protokolleditor geloeschtes Ereignis muss weiterhin als
    /// geloescht markiert werden - der Schutz gilt nur fuer die automatischen
    /// Rohrgrenzen.
    /// </summary>
    [Fact]
    public void Ein_entferntes_Ereignis_wird_weiterhin_als_geloescht_markiert()
    {
        var record = Record();
        var bleibt = Event("BABBB");
        bleibt.Entry.MeterStart = 4.82;
        var faellt = Event("BBCAA");
        faellt.Entry.MeterStart = 6.10;
        var events = new List<CodingEvent> { bleibt, faellt };

        void Uebernehmen() => CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: record,
                Events: events,
                ShowOverlay: false,
                HaltungslaengeM: 20.31),
            ThrowingActions(
                confirmEmptyProtocol: _ => true,
                addAutomaticBoundaryEvent: entry => events.Add(Event(entry)),
                assignProtocol: doc => record.Protocol = doc,
                markProjectDirty: () => { },
                syncCodingToPrimaryDamages: _ => { },
                persistCodingEventsAsTrainingSamples: _ => { },
                setBaselineSignature: _ => { },
                saveProjectAfterCoding: () => { },
                confirmMissingPipeEnd: _ => CodingApplyPipeEndDecision.Insert));

        Uebernehmen();
        events.Remove(faellt);
        Uebernehmen();

        var entries = record.Protocol!.Current!.Entries;
        Assert.True(entries.Single(e => e.Code == "BBCAA").IsDeleted);
        Assert.False(entries.Single(e => e.Code == "BABBB").IsDeleted);
    }

    private static CodingApplyChangesWorkflowActions ThrowingActions(
        Func<CodingApplyEmptyProtocolGuardResult, bool>? confirmEmptyProtocol = null,
        Action<ProtocolEntry>? addAutomaticBoundaryEvent = null,
        Action<ProtocolDocument>? assignProtocol = null,
        Action? markProjectDirty = null,
        Action<ProtocolDocument>? syncCodingToPrimaryDamages = null,
        Action<IReadOnlyList<CodingEvent>>? persistCodingEventsAsTrainingSamples = null,
        Action<string>? setBaselineSignature = null,
        Action? saveProjectAfterCoding = null,
        Action<string, TimeSpan>? showOverlay = null,
        Func<CodingApplyPipeEndPrompt, CodingApplyPipeEndDecision>? confirmMissingPipeEnd = null)
        => new(
            ConfirmEmptyProtocol: confirmEmptyProtocol ?? (_ => throw new InvalidOperationException("Confirm should not run.")),
            AddAutomaticBoundaryEvent: addAutomaticBoundaryEvent ?? (_ => throw new InvalidOperationException("Boundary event should not be added.")),
            AssignProtocol: assignProtocol ?? (_ => throw new InvalidOperationException("Assign should not run.")),
            MarkProjectDirty: markProjectDirty ?? (() => throw new InvalidOperationException("Dirty should not run.")),
            SyncCodingToPrimaryDamages: syncCodingToPrimaryDamages ?? (_ => throw new InvalidOperationException("Sync should not run.")),
            PersistCodingEventsAsTrainingSamples: persistCodingEventsAsTrainingSamples ?? (_ => throw new InvalidOperationException("Training should not run.")),
            SetBaselineSignature: setBaselineSignature ?? (_ => throw new InvalidOperationException("Baseline should not run.")),
            SaveProjectAfterCoding: saveProjectAfterCoding ?? (() => throw new InvalidOperationException("Save should not run.")),
            ShowOverlay: showOverlay ?? ((_, _) => throw new InvalidOperationException("Overlay should not run.")),
            ConfirmMissingPipeEnd: confirmMissingPipeEnd);

    private static HaltungRecord Record()
    {
        var record = new HaltungRecord();
        record.Fields["Haltungsname"] = "H-100";
        return record;
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = Guid.NewGuid(),
                Code = code
            }
        };

    private static CodingEvent Event(ProtocolEntry entry)
        => new()
        {
            Entry = entry,
            MeterAtCapture = entry.MeterStart ?? 0,
            VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
        };
}
