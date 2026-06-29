using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCaseWorkflowControllerTests
{
    [Fact]
    public async Task ProcessAsync_generiert_aktualisiert_ui_und_persistiert_samples()
    {
        var trainingCase = new TrainingCase
        {
            CaseId = "101.1-102.1",
            FolderPath = @"C:\Import\Case",
            VideoPath = @"C:\Import\Case\haltung.mp4",
            ProtocolPath = @"C:\Import\Case\protokoll.pdf",
            InspectionDate = new DateTime(2026, 6, 29)
        };
        var existingSignatures = new HashSet<string>(StringComparer.Ordinal) { "old" };
        var allSamples = new List<TrainingSample> { Sample("old", "AAA", "old") };
        var newSamples = new List<TrainingSample> { Sample("new-1", "BAA", "sig-new") };
        var runSummary = new TrainingBatchImportRunSummary();
        var previews = new List<TrainingBatchImportLivePreview>();
        var results = new List<SelfTrainingEntryResult>();
        var distributions = new List<(string Code, MatchLevel Level)>();
        var calls = new List<string>();
        TrainingCaseInput? generatedInput = null;
        IReadOnlyCollection<string>? generatedSignatures = null;
        List<TrainingSample>? savedSamples = null;

        var result = await TrainingBatchImportCaseWorkflowController.ProcessAsync(
            trainingCase,
            existingSignatures,
            allSamples,
            firstResultIndex: 7,
            processedCount: 5,
            runSummary,
            extractPreviewFrameAsync: (tc, _) =>
            {
                calls.Add($"extract:{tc.CaseId}");
                return Task.FromResult<string?>(@"C:\frames\preview.jpg");
            },
            generateWithDiagnosticsAsync: (input, signatures, _) =>
            {
                generatedInput = input;
                generatedSignatures = signatures;
                calls.Add($"generate:{input.CaseId}");
                return Task.FromResult(new TrainingSampleGenerationResult(
                    newSamples,
                    ParsedEntries: 1,
                    DuplicateSkipped: 0,
                    TrainingSampleGenerationOutcome.Success));
            },
            updateLivePreview: previews.Add,
            invokeOnUi: action =>
            {
                calls.Add("on-ui");
                action();
            },
            addResult: results.Add,
            updateCodeDistribution: (code, level) => distributions.Add((code, level)),
            saveSamplesAsync: samples =>
            {
                savedSamples = samples;
                calls.Add($"save:{samples.Count}");
                return Task.CompletedTask;
            },
            saveStateAsync: () =>
            {
                calls.Add("save-state");
                return Task.CompletedTask;
            },
            setSampleCount: value => calls.Add($"samples:{value}"),
            setCodesCovered: value => calls.Add($"codes:{value}"),
            log: message => calls.Add($"log:{message}"),
            CancellationToken.None);

        Assert.False(result.ShouldContinueWithNextCase);
        Assert.NotNull(generatedInput);
        Assert.Equal(trainingCase.CaseId, generatedInput.CaseId);
        Assert.Same(existingSignatures, generatedSignatures);
        Assert.Same(newSamples, savedSamples);
        Assert.Equal(2, allSamples.Count);
        Assert.Contains("sig-new", existingSignatures);
        Assert.Equal(1, runSummary.TotalNew);
        Assert.Equal(new[] { "Verarbeite...", "BAA" }, previews.Select(preview => preview.CodeInfo));
        Assert.Collection(
            results,
            entry =>
            {
                Assert.Equal(7, entry.Index);
                Assert.Equal("BAA", entry.VsaCode);
            });
        Assert.Equal(new[] { ("BAA", MatchLevel.NoFindings) }, distributions);
        Assert.Contains("extract:101.1-102.1", calls);
        Assert.Contains("generate:101.1-102.1", calls);
        Assert.Contains("save:1", calls);
        Assert.Contains("save-state", calls);
    }

    [Fact]
    public async Task ProcessAsync_ueberspringt_persistenz_wenn_keine_samples_entstehen()
    {
        var trainingCase = new TrainingCase { CaseId = "101.1-102.1" };
        var results = new List<SelfTrainingEntryResult>();
        var runSummary = new TrainingBatchImportRunSummary();
        var saveCalled = false;

        var result = await TrainingBatchImportCaseWorkflowController.ProcessAsync(
            trainingCase,
            new HashSet<string>(StringComparer.Ordinal),
            new List<TrainingSample>(),
            firstResultIndex: 3,
            processedCount: 1,
            runSummary,
            extractPreviewFrameAsync: (_, _) => Task.FromResult<string?>(null),
            generateWithDiagnosticsAsync: (_, _, _) => Task.FromResult(new TrainingSampleGenerationResult(
                [],
                ParsedEntries: 2,
                DuplicateSkipped: 2,
                TrainingSampleGenerationOutcome.OnlyDuplicates)),
            updateLivePreview: _ => { },
            invokeOnUi: action => action(),
            addResult: results.Add,
            updateCodeDistribution: (_, _) => { },
            saveSamplesAsync: _ =>
            {
                saveCalled = true;
                return Task.CompletedTask;
            },
            saveStateAsync: () => Task.CompletedTask,
            setSampleCount: _ => { },
            setCodesCovered: _ => { },
            log: _ => { },
            CancellationToken.None);

        Assert.True(result.ShouldContinueWithNextCase);
        Assert.False(saveCalled);
        Assert.Equal(0, runSummary.TotalNew);
        Assert.Equal(3, Assert.Single(results).Index);
    }

    private static TrainingSample Sample(string id, string code, string signature)
        => new()
        {
            SampleId = id,
            CaseId = "101.1-102.1",
            Code = code,
            Beschreibung = $"Sample {code}",
            MeterStart = 1.2,
            MeterEnd = 1.4,
            Signature = signature
        };
}
