using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingApplyControllerTests
{
    [Fact]
    public void Apply_preserves_the_existing_window_sequence()
    {
        var calls = new List<string>();
        var record = Record();
        var events = new List<CodingEvent> { Event("BAA") };
        var controller = new CodingApplyController(
            Bindings(
                record,
                events,
                calls,
                showOverlay: (message, duration) =>
                    calls.Add($"overlay:{message}:{duration.TotalSeconds}")));

        var applied = controller.Apply(showOverlay: true);

        Assert.True(applied);
        Assert.Equal(
            [
                "confirm-empty:False",
                "assign:1",
                "dirty:H-100",
                "sync:1",
                "dirty:H-100",
                "training:1",
                "baseline",
                "save",
                "overlay:1 Ereignisse in Prim\u00e4re Sch\u00e4den \u00fcbernommen:4"
            ],
            calls);
    }

    [Fact]
    public void ConfirmCanClose_applies_changes_without_overlay_when_user_chooses_apply()
    {
        var calls = new List<string>();
        var record = Record();
        var events = new List<CodingEvent> { Event("BAB") };
        var controller = new CodingApplyController(
            Bindings(
                record,
                events,
                calls,
                baselineSignature: "alter-stand",
                confirmUnappliedChanges: applyChanges =>
                {
                    calls.Add("confirm-close");
                    return applyChanges();
                },
                showOverlay: (_, _) => calls.Add("overlay")));

        var shouldClose = controller.ConfirmCanClose();

        Assert.True(shouldClose);
        Assert.Contains("confirm-close", calls);
        Assert.Contains("save", calls);
        Assert.DoesNotContain("overlay", calls);
    }

    [Fact]
    public void MarkProjectDirty_uses_the_current_haltung()
    {
        var calls = new List<string>();
        var record = Record();
        var controller = new CodingApplyController(Bindings(record, [], calls));

        controller.MarkProjectDirty();

        Assert.Equal(["dirty:H-100"], calls);
    }

    private static CodingApplyControllerBindings Bindings(
        HaltungRecord record,
        IReadOnlyList<CodingEvent> events,
        List<string> calls,
        string? baselineSignature = null,
        Func<Func<bool>, bool>? confirmUnappliedChanges = null,
        Action<string, TimeSpan>? showOverlay = null)
        => new(
            HasCodingViewModel: () => true,
            GetHaltungRecord: () => record,
            GetEventCollection: () => events,
            GetEvents: () => events,
            IsCodingMode: () => true,
            GetBaselineSignature: () => baselineSignature ?? CodingEventsSignatureBuilder.Build(events),
            ConfirmEmptyProtocol: guard =>
            {
                calls.Add($"confirm-empty:{guard.RequiresConfirmation}");
                return true;
            },
            AssignProtocol: document =>
            {
                record.Protocol = document;
                calls.Add($"assign:{document.Current!.Entries.Count}");
            },
            MarkProjectDirty: current => calls.Add($"dirty:{current?.GetFieldValue("Haltungsname")}"),
            SyncCodingToPrimaryDamages: document => calls.Add($"sync:{document.Current!.Entries.Count}"),
            PersistCodingEventsAsTrainingSamples: persisted => calls.Add($"training:{persisted.Count}"),
            SetBaselineSignature: _ => calls.Add("baseline"),
            SaveProjectAfterCoding: () => calls.Add("save"),
            ShowOverlay: showOverlay ?? ((_, _) => { }),
            ConfirmUnappliedChanges: confirmUnappliedChanges ?? (_ => throw new InvalidOperationException("Close confirmation should not run.")));

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
