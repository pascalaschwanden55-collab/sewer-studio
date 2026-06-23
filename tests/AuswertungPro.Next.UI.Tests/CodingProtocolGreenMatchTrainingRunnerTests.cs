using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolGreenMatchTrainingRunnerTests
{
    [Fact]
    public async Task AcceptGreenMatchesAsync_confirms_resolved_import_events_and_returns_overlay_for_accepted_count()
    {
        var first = Event("00000000-0000-0000-0000-000000000001", "BAB");
        var second = Event("00000000-0000-0000-0000-000000000002", "BAJ");
        var routing = Routing([Pair(first.Entry.EntryId), Pair(second.Entry.EntryId)]);
        var confirmed = new List<CodingEvent>();

        var overlay = await CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync(
            routing,
            [first, second],
            ev =>
            {
                confirmed.Add(ev);
                return Task.FromResult(ev == first);
            });

        Assert.Equal([first, second], confirmed);
        Assert.NotNull(overlay);
        Assert.Equal("1 gruene Treffer als Training uebernommen", overlay.Value.Text);
        Assert.Equal(TimeSpan.FromSeconds(4), overlay.Value.Duration);
    }

    [Fact]
    public async Task AcceptGreenMatchesAsync_returns_null_when_no_training_candidates_exist()
    {
        var routing = Routing([]);

        var overlay = await CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync(
            routing,
            [],
            _ => throw new InvalidOperationException("Confirm should not be called."));

        Assert.Null(overlay);
    }

    private static CodingEvent Event(string entryId, string code)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = Guid.Parse(entryId),
                Code = code,
                Source = ProtocolEntrySource.Imported
            }
        };

    private static CodingMatchRouting Routing(IReadOnlyList<BefundMatchPair> green)
        => new(new BefundMatchResult(), green, [], [], [], []);

    private static BefundMatchPair Pair(Guid gtRefId)
        => new(
            new BefundMatchFinding("BAB", 1.0, 1.0, "Riss", gtRefId.ToString()),
            new BefundMatchFinding("BAB", 1.0, 1.0, "Riss"),
            0.0,
            "gruen");
}
