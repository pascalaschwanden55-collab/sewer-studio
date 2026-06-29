using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCaseGenerationControllerTests
{
    [Fact]
    public async Task GenerateAsync_extracts_preview_and_generates_samples_from_case_input()
    {
        var inspectionDate = new DateTime(2026, 6, 29);
        var trainingCase = new TrainingCase
        {
            CaseId = "101.1-102.1",
            FolderPath = @"C:\Import\Case",
            VideoPath = @"C:\Import\Case\haltung.mp4",
            ProtocolPath = @"C:\Import\Case\protokoll.pdf",
            InspectionDate = inspectionDate
        };
        var existingSignatures = new HashSet<string>(StringComparer.Ordinal) { "old" };
        TrainingCaseInput? generatedInput = null;
        IReadOnlyCollection<string>? generatedSignatures = null;

        var result = await TrainingBatchImportCaseGenerationController.GenerateAsync(
            trainingCase,
            existingSignatures,
            (_, _) => Task.FromResult<string?>(@"C:\frames\preview.jpg"),
            (input, signatures, _) =>
            {
                generatedInput = input;
                generatedSignatures = signatures;
                return Task.FromResult(new TrainingSampleGenerationResult(
                    [new TrainingSample { Code = "BAA" }],
                    ParsedEntries: 1,
                    DuplicateSkipped: 0,
                    TrainingSampleGenerationOutcome.Success));
            },
            CancellationToken.None);

        Assert.NotNull(generatedInput);
        Assert.Equal("101.1-102.1", generatedInput.CaseId);
        Assert.Equal(@"C:\Import\Case", generatedInput.FolderPath);
        Assert.Equal(@"C:\Import\Case\haltung.mp4", generatedInput.VideoPath);
        Assert.Equal(@"C:\Import\Case\protokoll.pdf", generatedInput.ProtocolPath);
        Assert.Equal(inspectionDate, generatedInput.InspectionDate);
        Assert.Same(existingSignatures, generatedSignatures);
        Assert.Equal(@"C:\frames\preview.jpg", result.PreviewFrame);
        Assert.Equal("101.1-102.1", result.ProcessingPreview.CaseInfo);
        Assert.Equal("Verarbeite...", result.ProcessingPreview.CodeInfo);
        Assert.Equal("\u2014", result.ProcessingPreview.MeterInfo);
        Assert.Single(result.Generation.Samples);
    }
}
