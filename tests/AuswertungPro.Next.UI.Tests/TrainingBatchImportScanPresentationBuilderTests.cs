using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportScanPresentationBuilderTests
{
    [Fact]
    public void BuildSummary_formats_found_folder_and_protocol_counts()
    {
        var summary = TrainingBatchImportScanPresentationBuilder.BuildSummary(
            foundCount: 3,
            casesWithProtocolCount: 2);

        Assert.Equal("Gefunden: 3 Ordner, 2 mit Protokoll", summary);
    }

    [Fact]
    public void BuildCaseLine_formats_video_and_protocol_presence()
    {
        var withFiles = new TrainingCase
        {
            CaseId = "101.1-102.1",
            VideoPath = @"C:\Import\haltung.mp4",
            ProtocolPath = @"C:\Import\protokoll.pdf"
        };
        var withoutFiles = new TrainingCase { CaseId = "102.1-103.1" };

        Assert.Equal(
            "  101.1-102.1: Video, protokoll.pdf",
            TrainingBatchImportScanPresentationBuilder.BuildCaseLine(withFiles));
        Assert.Equal(
            "  102.1-103.1: kein Video, kein Protokoll",
            TrainingBatchImportScanPresentationBuilder.BuildCaseLine(withoutFiles));
    }
}
