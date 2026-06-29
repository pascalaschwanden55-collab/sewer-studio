using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportSamplePersistenceControllerTests
{
    [Fact]
    public async Task SaveCandidatesAsync_saves_before_appending_and_returns_updated_counters()
    {
        var existingSamples = new List<TrainingSample>
        {
            new() { Code = "AAA" }
        };
        var newSamples = new List<TrainingSample>
        {
            new() { Code = "BBB" },
            new() { Code = "AAA" }
        };
        var countWhenSaved = -1;
        List<TrainingSample>? savedSamples = null;

        var result = await TrainingBatchImportSamplePersistenceController.SaveCandidatesAsync(
            newSamples,
            existingSamples,
            samples =>
            {
                countWhenSaved = existingSamples.Count;
                savedSamples = samples;
                return Task.CompletedTask;
            });

        Assert.Same(newSamples, savedSamples);
        Assert.Equal(1, countWhenSaved);
        Assert.Equal(3, result.SampleCount);
        Assert.Equal(2, result.CodesCovered);
        Assert.Equal("2 Samples als Kandidaten gespeichert (Status: Neu). Freigabe ueber Review (Modul I) - KEIN Auto-Index.", result.CandidateLogMessage);
        Assert.Equal("  Gespeichert | Gesamt: 3 Samples, 2 Codes", result.StoredLogMessage);
    }
}
