using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCaseProgressPresentationBuilderTests
{
    [Fact]
    public void Build_formats_status_and_case_log_lines()
    {
        var trainingCase = new TrainingCase
        {
            CaseId = "101.1-102.1",
            ProtocolPath = @"C:\Import\protokoll.pdf",
            VideoPath = @"C:\Import\haltung.mp4"
        };

        var presentation = TrainingBatchImportCaseProgressPresentationBuilder.Build(
            zeroBasedIndex: 1,
            totalCount: 4,
            trainingCase);

        Assert.Equal("[2/4] 101.1-102.1...", presentation.StatusText);
        Assert.Equal(
            [
                "--- [2/4] 101.1-102.1 ---",
                @"  Protokoll: C:\Import\protokoll.pdf",
                @"  Video: C:\Import\haltung.mp4"
            ],
            presentation.LogLines);
    }

    [Fact]
    public void Build_formats_missing_video_as_keins()
    {
        var trainingCase = new TrainingCase
        {
            CaseId = "101.1-102.1",
            ProtocolPath = @"C:\Import\protokoll.pdf",
            VideoPath = ""
        };

        var presentation = TrainingBatchImportCaseProgressPresentationBuilder.Build(
            zeroBasedIndex: 0,
            totalCount: 1,
            trainingCase);

        Assert.Equal("  Video: keins", presentation.LogLines[2]);
    }
}
