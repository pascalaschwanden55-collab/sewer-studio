using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKbIndexRunnerTests
{
    [Fact]
    public async Task RunAsync_wenn_ollama_nicht_erreichbar_loggt_und_liefert_empty_outcome()
    {
        var logs = new List<string>();
        var runner = new TrainingKbIndexRunner(
            _ => Task.FromResult(false),
            () => throw new InvalidOperationException("Session darf nicht erstellt werden."),
            logs.Add,
            "KB-Update uebersprungen: Ollama nicht erreichbar auf http://localhost:11434/");

        var outcome = await runner.RunAsync(new[] { Sample("s1") }, CancellationToken.None);

        Assert.Empty(outcome.IndexedIds);
        Assert.Empty(outcome.SkippedIds);
        Assert.Equal(new[] { "KB-Update uebersprungen: Ollama nicht erreichbar auf http://localhost:11434/" }, logs);
    }

    [Fact]
    public async Task RunAsync_unterscheidet_indexed_skipped_und_transiente_fehler()
    {
        var session = new FakeKbIndexSession
        {
            AlreadyIndexedIds = { "already" },
            PermanentlySkippedIds = { "skip" },
            IndexResults =
            {
                ["new"] = true,
                ["failed"] = false
            }
        };
        var runner = new TrainingKbIndexRunner(
            _ => Task.FromResult(true),
            () => session,
            _ => { },
            "unreachable",
            () => new DateTime(2026, 6, 29, 12, 34, 0));

        var outcome = await runner.RunAsync(
            new[]
            {
                Sample("already"),
                Sample("skip"),
                Sample("new"),
                Sample("failed")
            },
            CancellationToken.None);

        Assert.Equal(new[] { "already", "new" }, outcome.IndexedIds);
        Assert.Equal(new[] { "skip" }, outcome.SkippedIds);
        Assert.Equal(new[] { "new", "failed" }, session.IndexAttempts);
        Assert.Equal(new[] { "Inkrementell 2026-06-29 12:34" }, session.VersionNotes);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task RunAsync_erstellt_keine_version_wenn_nichts_neu_indexiert_wurde()
    {
        var session = new FakeKbIndexSession
        {
            AlreadyIndexedIds = { "already" },
            IndexResults = { ["failed"] = false }
        };
        var runner = new TrainingKbIndexRunner(
            _ => Task.FromResult(true),
            () => session,
            _ => { },
            "unreachable");

        await runner.RunAsync(new[] { Sample("already"), Sample("failed") }, CancellationToken.None);

        Assert.Empty(session.VersionNotes);
    }

    [Fact]
    public async Task RunAsync_loggt_fehler_und_liefert_bisherigen_stand()
    {
        var logs = new List<string>();
        var session = new FakeKbIndexSession
        {
            AlreadyIndexedIds = { "already" },
            ThrowOnSampleId = "boom"
        };
        var runner = new TrainingKbIndexRunner(
            _ => Task.FromResult(true),
            () => session,
            logs.Add,
            "unreachable");

        var outcome = await runner.RunAsync(
            new[] { Sample("already"), Sample("boom"), Sample("after") },
            CancellationToken.None);

        Assert.Equal(new[] { "already" }, outcome.IndexedIds);
        Assert.Empty(outcome.SkippedIds);
        Assert.Equal(new[] { "KB-Update Fehler: sample boom" }, logs);
        Assert.True(session.Disposed);
    }

    private static TrainingSample Sample(string sampleId)
        => new()
        {
            SampleId = sampleId,
            Beschreibung = "Beschreibung fuer KB"
        };

    private sealed class FakeKbIndexSession : ITrainingKbIndexSession
    {
        public HashSet<string> AlreadyIndexedIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> PermanentlySkippedIds { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, bool> IndexResults { get; } = new(StringComparer.Ordinal);

        public List<string> IndexAttempts { get; } = new();

        public List<string> VersionNotes { get; } = new();

        public string? ThrowOnSampleId { get; set; }

        public bool Disposed { get; private set; }

        public bool IsIndexed(string sampleId)
            => AlreadyIndexedIds.Contains(sampleId);

        public bool IsPermanentlySkipped(TrainingSample sample)
            => PermanentlySkippedIds.Contains(sample.SampleId);

        public Task<bool> IndexSampleAsync(TrainingSample sample, CancellationToken ct)
        {
            if (sample.SampleId == ThrowOnSampleId)
                throw new InvalidOperationException($"sample {sample.SampleId}");

            IndexAttempts.Add(sample.SampleId);
            return Task.FromResult(IndexResults.GetValueOrDefault(sample.SampleId));
        }

        public void CreateVersion(string notes)
        {
            VersionNotes.Add(notes);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
