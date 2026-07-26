using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolTrainingCandidateResolverTests
{
    [Fact]
    public void ResolveImportEvents_skips_invalid_and_missing_refs_while_preserving_match_order()
    {
        var first = Event("00000000-0000-0000-0000-000000000001");
        var second = Event("00000000-0000-0000-0000-000000000002");
        var missing = Guid.Parse("00000000-0000-0000-0000-000000000099");

        var candidates = new[]
        {
            Pair("not-a-guid"),
            Pair(missing),
            Pair(second.Entry.EntryId),
            Pair(first.Entry.EntryId),
            Pair(second.Entry.EntryId)
        };

        var resolved = CodingProtocolTrainingCandidateResolver.ResolveImportEvents(
            candidates,
            [first, second]);

        Assert.Collection(
            resolved,
            item => Assert.Same(second, item),
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    private static CodingEvent Event(string entryId)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = Guid.Parse(entryId),
                Source = ProtocolEntrySource.Imported
            }
        };

    private static BefundMatchPair Pair(Guid gtRefId)
        => Pair(gtRefId.ToString());

    private static BefundMatchPair Pair(string? gtRefId)
        => new(
            new BefundMatchFinding("BAB", 1.0, 1.0, "Riss", gtRefId),
            new BefundMatchFinding("BAB", 1.0, 1.0, "Riss"),
            0.0,
            "gruen");
}
