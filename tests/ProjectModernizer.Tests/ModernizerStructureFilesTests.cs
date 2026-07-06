using AuswertungPro.Next.Domain.Models;
using Xunit;

public sealed class ModernizerStructureFilesTests
{
    [Fact]
    public void BuildFlatFieldTarget_keeps_expected_suffixes()
    {
        var holdingRoot = Path.Combine("root", "Haltungen_Verteilt", "06.1-07.2");

        var video = ModernizerStructureFiles.BuildFlatFieldTarget(
            raw: "kamera-g.mp4",
            source: "kamera-g.mp4",
            field: ModernizerProjectKeys.SecondaryVideoLink,
            holdingRoot,
            san: "06.1-07.2",
            stamp: "20250131",
            index: 0);

        var eigenPdf = ModernizerStructureFiles.BuildFlatFieldTarget(
            raw: "protokoll.pdf",
            source: "protokoll.pdf",
            field: FieldKeys.PdfEigen,
            holdingRoot,
            san: "06.1-07.2",
            stamp: "20250131",
            index: 0);

        var dpPdf = ModernizerStructureFiles.BuildFlatFieldTarget(
            raw: "protokoll_DP.pdf",
            source: "protokoll.pdf",
            field: FieldKeys.PdfPath,
            holdingRoot,
            san: "06.1-07.2",
            stamp: "20250131",
            index: 0);

        Assert.Equal("20250131_06.1-07.2-g.mp4", Path.GetFileName(video));
        Assert.Equal("20250131_06.1-07.2_E.pdf", Path.GetFileName(eigenPdf));
        Assert.Equal("20250131_06.1-07.2_DP.pdf", Path.GetFileName(dpPdf));
    }

    [Fact]
    public void BuildFlatLooseTarget_detects_dp_eigen_and_secondary_video_names()
    {
        var holdingRoot = Path.Combine("root", "Haltungen_Verteilt", "06.1-07.2");

        var dpPdf = ModernizerStructureFiles.BuildFlatLooseTarget("alt_DP.pdf", holdingRoot, "06.1-07.2", "20250131");
        var eigenPdf = ModernizerStructureFiles.BuildFlatLooseTarget("alt_Eigen.pdf", holdingRoot, "06.1-07.2", "20250131");
        var secondaryVideo = ModernizerStructureFiles.BuildFlatLooseTarget("film-g.mp4", holdingRoot, "06.1-07.2", "20250131");

        Assert.Equal("20250131_06.1-07.2_DP.pdf", Path.GetFileName(dpPdf));
        Assert.Equal("20250131_06.1-07.2_E.pdf", Path.GetFileName(eigenPdf));
        Assert.Equal("20250131_06.1-07.2-g.mp4", Path.GetFileName(secondaryVideo));
    }
}
