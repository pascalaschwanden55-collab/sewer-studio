using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

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
            ["confirm:False", "assign:2", "dirty", "sync:2", "dirty", "training:2", "baseline", "save", "overlay"],
            calls);
        Assert.NotNull(assigned);
        Assert.Same(assigned, synced);
        Assert.Equal(["BAA", "BAB"], assigned.Current!.Entries.Select(entry => entry.Code));
        Assert.Equal(CodingEventsSignatureBuilder.Build(events), baseline);
        Assert.Equal("2 Ereignisse in Prim\u00e4re Sch\u00e4den \u00fcbernommen", overlayMessage);
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
                assignProtocol: _ => { },
                markProjectDirty: () => { },
                syncCodingToPrimaryDamages: _ => { },
                persistCodingEventsAsTrainingSamples: _ => { },
                setBaselineSignature: _ => { },
                saveProjectAfterCoding: () => { }));

        Assert.Equal(CodingApplyChangesWorkflowOutcome.Applied, result.Outcome);
        Assert.True(result.Applied);
    }

    private static CodingApplyChangesWorkflowActions ThrowingActions(
        Func<CodingApplyEmptyProtocolGuardResult, bool>? confirmEmptyProtocol = null,
        Action<ProtocolDocument>? assignProtocol = null,
        Action? markProjectDirty = null,
        Action<ProtocolDocument>? syncCodingToPrimaryDamages = null,
        Action<IReadOnlyList<CodingEvent>>? persistCodingEventsAsTrainingSamples = null,
        Action<string>? setBaselineSignature = null,
        Action? saveProjectAfterCoding = null,
        Action<string, TimeSpan>? showOverlay = null)
        => new(
            ConfirmEmptyProtocol: confirmEmptyProtocol ?? (_ => throw new InvalidOperationException("Confirm should not run.")),
            AssignProtocol: assignProtocol ?? (_ => throw new InvalidOperationException("Assign should not run.")),
            MarkProjectDirty: markProjectDirty ?? (() => throw new InvalidOperationException("Dirty should not run.")),
            SyncCodingToPrimaryDamages: syncCodingToPrimaryDamages ?? (_ => throw new InvalidOperationException("Sync should not run.")),
            PersistCodingEventsAsTrainingSamples: persistCodingEventsAsTrainingSamples ?? (_ => throw new InvalidOperationException("Training should not run.")),
            SetBaselineSignature: setBaselineSignature ?? (_ => throw new InvalidOperationException("Baseline should not run.")),
            SaveProjectAfterCoding: saveProjectAfterCoding ?? (() => throw new InvalidOperationException("Save should not run.")),
            ShowOverlay: showOverlay ?? ((_, _) => throw new InvalidOperationException("Overlay should not run.")));

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
}
