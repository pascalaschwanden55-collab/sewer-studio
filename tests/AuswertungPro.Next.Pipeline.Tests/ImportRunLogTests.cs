using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Pipeline.Tests;

public class ImportRunLogTests
{
    [Fact]
    public void AddEntry_AccumulatesEntries()
    {
        var log = new ImportRunLog { ImportType = "Test" };

        log.AddEntry("Phase1", "Op1", ImportLogStatus.Created, recordKey: "H1");
        log.AddEntry("Phase1", "Op2", ImportLogStatus.Updated, recordKey: "H2");
        log.AddEntry("Phase1", "Op3", ImportLogStatus.Error, recordKey: "H3", detail: "Fehler");

        Assert.Equal(3, log.Entries.Count);
        Assert.Equal(1, log.TotalCreated);
        Assert.Equal(1, log.TotalUpdated);
        Assert.Equal(1, log.TotalErrors);
        Assert.Equal(0, log.TotalConflicts);
        Assert.Equal(0, log.TotalSkipped);
    }

    [Fact]
    public void Complete_SetsDuration()
    {
        var log = new ImportRunLog { ImportType = "Test" };
        Assert.Null(log.CompletedAtUtc);

        log.Complete();

        Assert.NotNull(log.CompletedAtUtc);
        Assert.True(log.Duration.TotalMilliseconds >= 0);
    }

    [Fact]
    public void EntriesList_ReturnsSortedByTimestamp()
    {
        var log = new ImportRunLog();
        log.AddEntry("A", "First", ImportLogStatus.Info);
        log.AddEntry("B", "Second", ImportLogStatus.Created);
        log.AddEntry("C", "Third", ImportLogStatus.Updated);

        var sorted = log.EntriesList;
        Assert.Equal(3, sorted.Count);
        for (var i = 1; i < sorted.Count; i++)
        {
            Assert.True(sorted[i].TimestampUtc >= sorted[i - 1].TimestampUtc);
        }
    }

    [Fact]
    public void RunId_Is12Chars()
    {
        var log = new ImportRunLog();
        Assert.Equal(12, log.RunId.Length);
    }

    [Fact]
    public void WasDryRun_DefaultsFalse()
    {
        var log = new ImportRunLog();
        Assert.False(log.WasDryRun);
        Assert.False(log.WasCancelled);
    }

    [Fact]
    public async Task ThreadSafe_ConcurrentAdds()
    {
        var log = new ImportRunLog();
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => log.AddEntry("P", $"Op{i}", ImportLogStatus.Updated, recordKey: $"R{i}"))
        ).ToArray();

        await Task.WhenAll(tasks);
        Assert.Equal(100, log.Entries.Count);
        Assert.Equal(100, log.TotalUpdated);
    }
}
