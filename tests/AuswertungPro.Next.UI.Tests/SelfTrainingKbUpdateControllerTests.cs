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
