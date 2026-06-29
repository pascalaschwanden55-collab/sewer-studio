using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportSampleLogBuilderTests
{
    [Fact]
    public void Build_formats_summary_and_sample_lines()
    {
        var samples = new[]
        {
            new TrainingSample
            {
                Code = "BAA",
                MeterStart = 1.25,
                Status = TrainingSampleStatus.New,
                Beschreibung = "Riss"
            },
            new TrainingSample
            {
                Code = "BBB",
                MeterStart = 3,
                Status = TrainingSampleStatus.New,
                Beschreibung = "Ablagerung"
            }
        };

        var lines = TrainingBatchImportSampleLogBuilder.Build(samples);

        Assert.Equal(
            [
                "  -> 2 Samples (Status: Neu, Freigabe ueber Review):",
                "     BAA @ 1.25m [New] - Riss",
                "     BBB @ 3.00m [New] - Ablagerung"
            ],
            lines);
    }
}
