using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

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

    [JunctionFact]
    public void Detect_IgnoriertVerknuepftenDataOrdner()
    {
        var exportRoot = Path.Combine(_tempDirectory, "export");
        var externalData = Path.Combine(_tempDirectory, "external-data");
        var dataLink = Path.Combine(exportRoot, "Data");
        Directory.CreateDirectory(exportRoot);
        Directory.CreateDirectory(externalData);
        File.WriteAllText(Path.Combine(externalData, "Arizona.fdb"), "fremd");
        var filmDirectory = Path.Combine(exportRoot, "Film");
        Directory.CreateDirectory(filmDirectory);
        File.WriteAllText(Path.Combine(filmDirectory, "Daten.txt"), "IBAK");
        JunctionTestSupport.CreateDirectoryLink(dataLink, externalData);

        try
        {
            var result = new KiasExportPatternDetectionService().Detect(exportRoot);

            Assert.False(result.HasArizonaFdb);
            Assert.False(result.IsKias);
        }
        finally
        {
            DeleteDirectoryLink(dataLink);
        }
    }

    [JunctionFact]
    public void Detect_IgnoriertVerknuepftenFilmordner()
    {
        var exportRoot = Path.Combine(_tempDirectory, "export");
        var externalFilm = Path.Combine(_tempDirectory, "external-film");
        var filmLink = Path.Combine(exportRoot, "Film");
        Directory.CreateDirectory(exportRoot);
        Directory.CreateDirectory(externalFilm);
        File.WriteAllText(Path.Combine(exportRoot, "Arizona.fdb"), "fdb");
        File.WriteAllText(Path.Combine(externalFilm, "Daten.txt"), "IBAK");
        File.WriteAllText(Path.Combine(externalFilm, "H_100-200~G.mpg"), "fremd");
        var reportDirectory = Path.Combine(exportRoot, "Report");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(Path.Combine(reportDirectory, "H_100-200.pdf"), "bericht");
        JunctionTestSupport.CreateDirectoryLink(filmLink, externalFilm);

        try
        {
            var result = new KiasExportPatternDetectionService().Detect(exportRoot);

            Assert.False(result.HasFilmFolder);
            Assert.False(result.HasDatenTxt);
            Assert.Equal(0, result.GegenrichtungVideoCount);
            Assert.False(result.IsKias);
        }
        finally
        {
            DeleteDirectoryLink(filmLink);
        }
    }

    [JunctionFact]
    public void Detect_IgnoriertVerknuepftenReportordner()
    {
        var exportRoot = Path.Combine(_tempDirectory, "export");
        var externalReport = Path.Combine(_tempDirectory, "external-report");
        var reportLink = Path.Combine(exportRoot, "Report");
        Directory.CreateDirectory(exportRoot);
        Directory.CreateDirectory(externalReport);
        File.WriteAllText(Path.Combine(exportRoot, "Arizona.fdb"), "fdb");
        Directory.CreateDirectory(Path.Combine(exportRoot, "Film"));
        File.WriteAllText(Path.Combine(externalReport, "H_100-200.pdf"), "fremd");
        File.WriteAllText(Path.Combine(externalReport, "L_300-400.pdf"), "fremd");
        JunctionTestSupport.CreateDirectoryLink(reportLink, externalReport);

        try
        {
            var result = new KiasExportPatternDetectionService().Detect(exportRoot);

            Assert.False(result.HasReportFolder);
            Assert.Equal(0, result.HoldingPdfCount);
            Assert.Equal(0, result.LateralPdfCount);
            Assert.False(result.IsKias);
        }
        finally
        {
            DeleteDirectoryLink(reportLink);
        }
    }

    [JunctionFact]
    public void Detect_IgnoriertVerknuepfteDirekteDateien()
    {
        var exportRoot = Path.Combine(_tempDirectory, "export");
        var externalRoot = Path.Combine(_tempDirectory, "external-files");
        var filmDirectory = Path.Combine(exportRoot, "Film");
        var reportDirectory = Path.Combine(exportRoot, "Report");
        Directory.CreateDirectory(filmDirectory);
        Directory.CreateDirectory(reportDirectory);
        Directory.CreateDirectory(externalRoot);

        var externalFdb = CreateExternalFile(externalRoot, "Arizona.fdb", "fdb");
        var externalData = CreateExternalFile(externalRoot, "Daten.txt", "IBAK");
        var externalGegenrichtung = CreateExternalFile(externalRoot, "gegenrichtung.mpg", "video");
        var externalWiederholung = CreateExternalFile(externalRoot, "wiederholung.mpg", "video");
        var externalHoldingPdf = CreateExternalFile(externalRoot, "haltung.pdf", "pdf");
        var externalLateralPdf = CreateExternalFile(externalRoot, "lateral.pdf", "pdf");

        var fileLinks = new[]
        {
            (Path.Combine(exportRoot, "Arizona.fdb"), externalFdb),
            (Path.Combine(filmDirectory, "Daten.txt"), externalData),
            (Path.Combine(filmDirectory, "H_100-200~G.mpg"), externalGegenrichtung),
            (Path.Combine(filmDirectory, "H_100-200~1.mpg"), externalWiederholung),
            (Path.Combine(reportDirectory, "H_100-200.pdf"), externalHoldingPdf),
            (Path.Combine(reportDirectory, "L_300-400.pdf"), externalLateralPdf),
        };
        foreach (var (link, target) in fileLinks)
            File.CreateSymbolicLink(link, target);

        try
        {
            var result = new KiasExportPatternDetectionService().Detect(exportRoot);

            Assert.False(result.HasArizonaFdb);
            Assert.True(result.HasFilmFolder);
            Assert.True(result.HasReportFolder);
            Assert.False(result.HasDatenTxt);
            Assert.Equal(0, result.HoldingPdfCount);
            Assert.Equal(0, result.LateralPdfCount);
            Assert.Equal(0, result.GegenrichtungVideoCount);
            Assert.Equal(0, result.RepeatTakeVideoCount);
            Assert.False(result.IsKias);
        }
        finally
        {
            foreach (var (link, _) in fileLinks)
                DeleteFileLink(link);
        }
    }

    [JunctionFact]
    public void Detect_LiestAusdruecklichGewaehlteVerknuepfteWurzel()
    {
        var targetRoot = Path.Combine(_tempDirectory, "target");
        var selectedRoot = Path.Combine(_tempDirectory, "selected-link");
        CreateCompleteKiasFolder(targetRoot);
        JunctionTestSupport.CreateDirectoryLink(selectedRoot, targetRoot);

        try
        {
            var result = KiasExportPattern.Detect(selectedRoot);

            AssertCompleteDetection(result);
        }
        finally
        {
            DeleteDirectoryLink(selectedRoot);
        }
    }

    private void CreateCompleteKiasFolder()
        => CreateCompleteKiasFolder(_tempDirectory);

    private static void CreateCompleteKiasFolder(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Arizona.fdb"), string.Empty);

        var filmDirectory = Path.Combine(root, "Film");
        Directory.CreateDirectory(filmDirectory);
        File.WriteAllText(Path.Combine(filmDirectory, "Daten.txt"), "IBAK");
        File.WriteAllText(Path.Combine(filmDirectory, "H_100-200.mpg"), string.Empty);
        File.WriteAllText(Path.Combine(filmDirectory, "H_100-200~G.mpg"), string.Empty);
        File.WriteAllText(Path.Combine(filmDirectory, "H_100-200~1.mpg"), string.Empty);

        var reportDirectory = Path.Combine(root, "Report");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(Path.Combine(reportDirectory, "H_100-200.pdf"), string.Empty);
        File.WriteAllText(Path.Combine(reportDirectory, "L_300-400.pdf"), string.Empty);
    }

    private static string CreateExternalFile(string root, string name, string contents)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, contents);
        return path;
    }

    private static void DeleteDirectoryLink(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path);
        }
        catch
        {
            // Nur Test-Aufraeumen.
        }
    }

    private static void DeleteFileLink(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Nur Test-Aufraeumen.
        }
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
