using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class KanalExportDetectionServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"KanalExportDetectionServiceTests_{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_ReturnsUnknownForMissingFolder()
    {
        IKanalExportDetectionService detector = new KanalExportDetectionService();

        var result = detector.Detect(Path.Combine(_tempDirectory, "fehlt"));

        Assert.Equal(KanalExportFormat.Unknown, result.Format);
        Assert.Equal("Pfad nicht vorhanden oder leer", result.Reason);
    }

    [Fact]
    public void ImportOrchestrator_UsesInjectedExportDetector()
    {
        var sourceDirectory = Path.Combine(_tempDirectory, "source");
        var projectDirectory = Path.Combine(_tempDirectory, "project");
        Directory.CreateDirectory(sourceDirectory);
        var detector = new RecordingExportDetector();
        var orchestrator = new ProjectImportOrchestrator(
            new XtfImportServiceAdapter(),
            new WinCanDbImportService(),
            exportDetector: detector);

        var result = orchestrator.Import(sourceDirectory, projectDirectory, new Project());

        Assert.Equal(KanalExportFormat.Unknown, result.Format);
        Assert.Equal(1, detector.Calls);
        Assert.Equal(sourceDirectory, detector.LastSourceFolder);
    }

    [Fact]
    public void InstanceService_UsesInjectedKiasPatternDetector()
    {
        Directory.CreateDirectory(_tempDirectory);
        var kiasDetector = new RecordingKiasPatternDetector();
        var detector = new KanalExportDetectionService(kiasDetector);

        var result = detector.Detect(_tempDirectory);

        Assert.Equal(KanalExportFormat.Ibak, result.Format);
        Assert.Equal(1, kiasDetector.Calls);
        Assert.Equal(_tempDirectory, kiasDetector.LastSourceFolder);
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

    private sealed class RecordingExportDetector : IKanalExportDetectionService
    {
        public int Calls { get; private set; }
        public string? LastSourceFolder { get; private set; }

        public KanalExportDetection Detect(string sourceFolder)
        {
            Calls++;
            LastSourceFolder = sourceFolder;
            return new KanalExportDetection(
                KanalExportFormat.Unknown,
                null,
                null,
                null,
                "Test-Ergebnis");
        }
    }

    private sealed class RecordingKiasPatternDetector : IKiasExportPatternDetector
    {
        public int Calls { get; private set; }
        public string? LastSourceFolder { get; private set; }

        public KiasExportDetectionResult Detect(string exportRoot)
        {
            Calls++;
            LastSourceFolder = exportRoot;
            return new KiasExportDetectionResult(
                IsKias: true,
                HasArizonaFdb: true,
                HasFilmFolder: true,
                HasReportFolder: false,
                HasDatenTxt: true,
                HoldingPdfCount: 0,
                LateralPdfCount: 0,
                GegenrichtungVideoCount: 0,
                RepeatTakeVideoCount: 0,
                Reason: "Test-KIAS");
        }
    }
}
