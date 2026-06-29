using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingKbUpdateControllerTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    public void ShouldRun_requires_exact_matches_and_generated_samples(
        int exactMatches,
        int samplesGenerated,
        bool expected)
    {
        Assert.Equal(
            expected,
            SelfTrainingKbUpdateController.ShouldRun(Result(exactMatches, samplesGenerated)));
    }

    [Fact]
    public void SelectApprovedSamplesForRun_filtert_case_und_approved_status()
    {
        var samples = new[]
        {
            Sample("keep", "H-001", TrainingSampleStatus.Approved),
            Sample("skip-case", "H-002", TrainingSampleStatus.Approved),
            Sample("skip-status", "H-001", TrainingSampleStatus.New)
        };

        var selected = SelfTrainingKbUpdateController.SelectApprovedSamplesForRun(
            samples,
            Result(caseId: "H-001"));

        Assert.Equal(new[] { "keep" }, selected.Select(s => s.SampleId));
    }

    [Fact]
    public void MarkPendingBeforeIndex_setzt_nur_none_und_error_auf_pending()
    {
        var samples = new[]
        {
            Sample("none", kbState: KbIndexState.None),
            Sample("error", kbState: KbIndexState.Error),
            Sample("indexed", kbState: KbIndexState.Indexed),
            Sample("skipped", kbState: KbIndexState.Skipped)
        };

        SelfTrainingKbUpdateController.MarkPendingBeforeIndex(samples);

        Assert.Equal(KbIndexState.Pending, samples[0].KbIndexState);
        Assert.Equal(KbIndexState.Pending, samples[1].KbIndexState);
        Assert.Equal(KbIndexState.Indexed, samples[2].KbIndexState);
        Assert.Equal(KbIndexState.Skipped, samples[3].KbIndexState);
    }

    [Fact]
    public void ApplyOutcome_mappt_indexed_skipped_und_pending_fehler()
    {
        var samples = new[]
        {
            Sample("indexed", kbState: KbIndexState.Pending),
            Sample("skipped", kbState: KbIndexState.Pending),
            Sample("failed", kbState: KbIndexState.Pending),
            Sample("already-indexed", kbState: KbIndexState.Indexed)
        };
        var outcome = new KbIndexOutcome(
            new[] { "indexed" },
            new[] { "skipped" });

        SelfTrainingKbUpdateController.ApplyOutcome(samples, outcome);

        Assert.Equal(KbIndexState.Indexed, samples[0].KbIndexState);
        Assert.Equal(KbIndexState.Skipped, samples[1].KbIndexState);
        Assert.Equal(KbIndexState.Error, samples[2].KbIndexState);
        Assert.Equal(KbIndexState.Indexed, samples[3].KbIndexState);
    }

    [Fact]
    public void BuildStartLogMessage_formatiert_bisherige_meldung()
    {
        Assert.Equal(
            "3 ExactMatch-Samples \u2014 starte KB-Update...",
            SelfTrainingKbUpdateController.BuildStartLogMessage(3));
    }

    [Fact]
    public async Task RunApprovedSamplesUpdateAsync_ueberspringt_io_wenn_keine_kb_aktualisierung_noetig_ist()
    {
        var loadCalled = false;

        await SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(
            Result(exactMatches: 0, samplesGenerated: 1),
            () =>
            {
                loadCalled = true;
                return Task.FromResult(new List<TrainingSample>());
            },
            _ => throw new InvalidOperationException("Merge darf nicht laufen."),
            (_, _) => throw new InvalidOperationException("Index darf nicht laufen."),
            _ => throw new InvalidOperationException("Log darf nicht laufen."),
            CancellationToken.None);

        Assert.False(loadCalled);
    }

    [Fact]
    public async Task RunApprovedSamplesUpdateAsync_markiert_pending_indexiert_und_persistiert_zweimal()
    {
        var logs = new List<string>();
        var mergeSnapshots = new List<Dictionary<string, KbIndexState>>();
        List<TrainingSample>? indexedSamples = null;
        var samples = new List<TrainingSample>
        {
            Sample("indexed", kbState: KbIndexState.None),
            Sample("failed", kbState: KbIndexState.Error),
            Sample("other-case", caseId: "H-002", kbState: KbIndexState.None),
            Sample("new-status", status: TrainingSampleStatus.New, kbState: KbIndexState.None)
        };

        await SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(
            Result(exactMatches: 2, samplesGenerated: 2, caseId: "H-001"),
            () => Task.FromResult(samples),
            toMerge =>
            {
                mergeSnapshots.Add(toMerge.ToDictionary(s => s.SampleId, s => s.KbIndexState));
                return Task.CompletedTask;
            },
            (toIndex, _) =>
            {
                indexedSamples = toIndex.ToList();
                return Task.FromResult(new KbIndexOutcome(new[] { "indexed" }, Array.Empty<string>()));
            },
            logs.Add,
            CancellationToken.None);

        Assert.Equal(new[] { "indexed", "failed" }, indexedSamples!.Select(s => s.SampleId));
        Assert.Equal(new[] { "2 ExactMatch-Samples \u2014 starte KB-Update..." }, logs);
        Assert.Equal(2, mergeSnapshots.Count);
        Assert.Equal(KbIndexState.Pending, mergeSnapshots[0]["indexed"]);
        Assert.Equal(KbIndexState.Pending, mergeSnapshots[0]["failed"]);
        Assert.Equal(KbIndexState.Indexed, mergeSnapshots[1]["indexed"]);
        Assert.Equal(KbIndexState.Error, mergeSnapshots[1]["failed"]);
    }

    private static SelfTrainingResult Result(
        int exactMatches = 1,
        int samplesGenerated = 1,
        string caseId = "H-001")
        => new(
            caseId,
            TotalEntries: exactMatches,
            ExactMatches: exactMatches,
            PartialMatches: 0,
            Mismatches: 0,
            NoFindings: 0,
            OverallTechnique: null,
            Duration: TimeSpan.Zero,
            SamplesGenerated: samplesGenerated);

    private static TrainingSample Sample(
        string sampleId,
        string caseId = "H-001",
        TrainingSampleStatus status = TrainingSampleStatus.Approved,
        KbIndexState kbState = KbIndexState.None)
        => new()
        {
            SampleId = sampleId,
            CaseId = caseId,
            Status = status,
            KbIndexState = kbState
        };
}
