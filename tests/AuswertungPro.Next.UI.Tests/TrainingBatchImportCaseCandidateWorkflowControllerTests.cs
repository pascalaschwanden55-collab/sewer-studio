using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCaseCandidateWorkflowControllerTests
{
    [Fact]
    public void Apply_verarbeitet_sample_generation_und_signalisiert_persistenz()
    {
        var summary = new TrainingBatchImportRunSummary();
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        var samples = new List<TrainingSample>
        {
            new()
            {
                Code = "BAA",
                Beschreibung = "Riss",
                MeterStart = 1.25,
                MeterEnd = 1.75,
                FramePath = "sample.jpg",
                Signature = "sig-a"
            }
        };
        var generation = new TrainingSampleGenerationResult(
            samples,
            ParsedEntries: 1,
            DuplicateSkipped: 0,
            TrainingSampleGenerationOutcome.Success);
        var caseGeneration = new TrainingBatchImportCaseGenerationResult(
            PreviewFrame: "preview.jpg",
            ProcessingPreview: new TrainingBatchImportLivePreview("case", "processing", "meter", "processing.jpg"),
            Generation: generation);
        var calls = new List<string>();

        var result = TrainingBatchImportCaseCandidateWorkflowController.Apply(
            caseId: "101.1-102.1",
            caseGeneration,
            nextResultIndex: 4,
            signatures,
            summary,
            preview => calls.Add($"preview:{preview.CodeInfo}:{preview.FramePath}"),
            action =>
            {
                calls.Add("on-ui");
                action();
            },
            entry => calls.Add($"add-result:{entry.Index}:{entry.VsaCode}"),
            (code, level) => calls.Add($"distribution:{code}:{level}"),
            calls.Add);

        Assert.True(result.ShouldPersist);
        Assert.Same(samples, result.NewSamples);
        Assert.Contains("1 Kandidaten gespeichert", summary.BuildCompletionStatus());
        Assert.Equal(new[] { "sig-a" }, signatures.ToArray());
        Assert.Equal(
            new[]
            {
                "preview:processing:processing.jpg",
                "preview:BAA:sample.jpg",
                "on-ui",
                "add-result:4:BAA",
                "distribution:BAA:NoFindings",
                "  -> 1 Samples (Status: Neu, Freigabe ueber Review):",
                "     BAA @ 1.25m [New] - Riss"
            },
            calls);
    }

    [Fact]
    public void Apply_verarbeitet_skip_generation_und_signalisiert_naechsten_case()
    {
        var summary = new TrainingBatchImportRunSummary();
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        var generation = new TrainingSampleGenerationResult(
            [],
            ParsedEntries: 0,
            DuplicateSkipped: 0,
            TrainingSampleGenerationOutcome.NoProtocolEntries);
        var caseGeneration = new TrainingBatchImportCaseGenerationResult(
            PreviewFrame: "preview.jpg",
            ProcessingPreview: new TrainingBatchImportLivePreview("case", "processing", "meter", "processing.jpg"),
            Generation: generation);
        var calls = new List<string>();

        var result = TrainingBatchImportCaseCandidateWorkflowController.Apply(
            caseId: "101.1-102.1",
            caseGeneration,
            nextResultIndex: 8,
            signatures,
            summary,
            preview => calls.Add($"preview:{preview.CodeInfo}:{preview.FramePath}"),
            action =>
            {
                calls.Add("on-ui");
                action();
            },
            entry => calls.Add($"add-result:{entry.Index}:{entry.VsaCode}:{entry.Summary}"),
            (_, _) => calls.Add("distribution"),
            calls.Add);

        Assert.False(result.ShouldPersist);
        Assert.Empty(result.NewSamples);
        Assert.Contains("1 ohne Eintraege.", summary.BuildNoNewStatus(processedCaseCount: 1));
        Assert.Empty(signatures);
        Assert.Equal(
            new[]
            {
                "preview:processing:processing.jpg",
                "  -> 0 Samples (keine Protokolleintraege erkannt)",
                "preview:\u2014:preview.jpg",
                "on-ui",
                "add-result:8:101.1-102.1:keine Eintraege"
            },
            calls);
    }
}
