using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchRunnerTests
{
    [Fact]
    public void Run_matches_import_and_coding_events_and_rebuilds_buckets()
    {
        var importId = Guid.Parse("00000000-0000-0000-0000-000000000101");
        var codingId = Guid.Parse("00000000-0000-0000-0000-000000000102");
        var oldId = Guid.Parse("00000000-0000-0000-0000-000000000199");
        var importEvents = new[] { Event(importId, "BAB", 1.0) };
        var codingEvents = new[] { Event(codingId, "BAB", 1.0) };
        var buckets = new Dictionary<Guid, CodingProtocolMatchBucket>
        {
            [oldId] = CodingProtocolMatchBucket.FalseAlarm
        };

        var routing = CodingProtocolMatchRunner.Run(importEvents, codingEvents, buckets);

        Assert.Single(routing.Trainingskandidaten);
        Assert.False(buckets.ContainsKey(oldId));
        Assert.Equal(CodingProtocolMatchBucket.TrainingGreen, buckets[importId]);
        Assert.Equal(CodingProtocolMatchBucket.TrainingGreen, buckets[codingId]);
    }

    private static CodingEvent Event(Guid id, string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = id,
                Code = code,
                MeterStart = meter,
                MeterEnd = meter,
                Beschreibung = code
            }
        };
}
