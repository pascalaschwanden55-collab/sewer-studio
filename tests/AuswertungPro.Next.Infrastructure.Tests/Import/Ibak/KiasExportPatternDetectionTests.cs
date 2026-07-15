using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Tests.Import.Ibak;

public sealed class KiasExportPatternDetectionTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"KiasExportPatternDetectionTests_{Guid.NewGuid():N}");

    [Fact]
    public void Detect_ErkenntVollstaendigenKiasOrdnerUndZaehltBerichteUndAufnahmen()
    {
        CreateCompleteKiasFolder();

        var result = KiasExportPattern.Detect(_tempDirectory);

        AssertCompleteDetection(result);
    }

    [Fact]
    public void InstanceService_ErkenntDieselbenKiasMerkmale()
    {
        CreateCompleteKiasFolder();
        var detector = new KiasExportPatternDetectionService();

        var result = detector.Detect(_tempDirectory);

        Assert.True(result.IsKias);
        Assert.True(result.HasArizonaFdb);
        Assert.True(result.HasFilmFolder);
        Assert.True(result.HasReportFolder);
        Assert.True(result.HasDatenTxt);
        Assert.Equal(1, result.HoldingPdfCount);
        Assert.Equal(1, result.LateralPdfCount);
        Assert.Equal(1, result.GegenrichtungVideoCount);
        Assert.Equal(1, result.RepeatTakeVideoCount);
        Assert.Equal("KIAS erkannt: Arizona.fdb + Film/ + Daten.txt", result.Reason);
    }

    private void CreateCompleteKiasFolder()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "Arizona.fdb"), string.Empty);

        var filmDirectory = Path.Combine(_tempDirectory, "Film");
        Directory.CreateDirectory(filmDirectory);
        File.WriteAllText(Path.Combine(filmDirectory, "Daten.txt"), "IBAK");
        File.WriteAllText(Path.Combine(filmDirectory, "H_100-200.mpg"), string.Empty);
        File.WriteAllText(Path.Combine(filmDirectory, "H_100-200~G.mpg"), string.Empty);
        File.WriteAllText(Path.Combine(filmDirectory, "H_100-200~1.mpg"), string.Empty);

        var reportDirectory = Path.Combine(_tempDirectory, "Report");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(Path.Combine(reportDirectory, "H_100-200.pdf"), string.Empty);
        File.WriteAllText(Path.Combine(reportDirectory, "L_300-400.pdf"), string.Empty);
    }

    private static void AssertCompleteDetection(KiasExportPattern.DetectionResult result)
    {
        Assert.True(result.IsKias);
        Assert.True(result.HasArizonaFdb);
        Assert.True(result.HasFilmFolder);
        Assert.True(result.HasReportFolder);
        Assert.True(result.HasDatenTxt);
        Assert.Equal(1, result.HoldingPdfCount);
        Assert.Equal(1, result.LateralPdfCount);
        Assert.Equal(1, result.GegenrichtungVideoCount);
        Assert.Equal(1, result.RepeatTakeVideoCount);
        Assert.Equal("KIAS erkannt: Arizona.fdb + Film/ + Daten.txt", result.Reason);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist best effort.
        }
    }
}
