using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class WinCanImportNegativeTests
{
    [Theory]
    [InlineData("H_123.mp4", true)]
    [InlineData("H_1234.mp4", false)]
    public void SdfFallback_VerknuepftVideoNurAnEchterHaltungsgrenze(
        string videoName,
        bool expectedLinked)
    {
        var root = Path.Combine(Path.GetTempPath(), $"wincan-video-boundary-{Guid.NewGuid():N}");
        var dbDirectory = Path.Combine(root, "DB");
        var videoDirectory = Path.Combine(root, "Video");
        Directory.CreateDirectory(dbDirectory);
        Directory.CreateDirectory(videoDirectory);
        File.WriteAllText(Path.Combine(dbDirectory, "projekt.sdf"), "sdf");
        File.WriteAllText(Path.Combine(root, "export.xtf"), "xtf");
        var videoPath = Path.Combine(videoDirectory, videoName);
        File.WriteAllText(videoPath, "video");
        var project = new Project();
        var record = project.CreateNewRecord();
        record.SetFieldValue("Haltungsname", "123", FieldSource.Xtf, userEdited: false);
        project.AddRecord(record);
        var xtf = new SuccessfulXtfImport();

        try
        {
            var result = new WinCanDbImportService(new UnusedM150Reader(), xtf)
                .ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            Assert.True(xtf.Called);
            if (expectedLinked)
                Assert.Equal(videoPath, record.GetFieldValue("Link"), ignoreCase: true);
            else
                Assert.True(string.IsNullOrWhiteSpace(record.GetFieldValue("Link")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [JunctionFact]
    public void ImportWinCanExport_BetrittKeineUntergeordneteVerzeichnisverknuepfung_AberAkzeptiertSieAlsExpliziteWurzel()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"wincan-link-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(testRoot, "quelle");
        var externalRoot = Path.Combine(testRoot, "extern");
        var externalDb = Path.Combine(externalRoot, "DB", "projekt.db3");
        var link = Path.Combine(sourceRoot, "verknuepft");
        var mediaRoot = Path.Combine(testRoot, "medien-quelle");
        var externalMediaRoot = Path.Combine(testRoot, "medien-extern");
        var videoLink = Path.Combine(mediaRoot, "Video");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(externalDb)!);
        var original = "Das ist keine SQLite-Datenbank."u8.ToArray();
        File.WriteAllBytes(externalDb, original);
        JunctionTestSupport.CreateDirectoryLink(link, externalRoot);
        Directory.CreateDirectory(Path.Combine(mediaRoot, "DB"));
        Directory.CreateDirectory(externalMediaRoot);
        File.WriteAllText(Path.Combine(mediaRoot, "DB", "projekt.sdf"), "sdf");
        File.WriteAllText(Path.Combine(mediaRoot, "export.xtf"), "xtf");
        File.WriteAllText(Path.Combine(externalMediaRoot, "H_123.mp4"), "fremdes-video");
        JunctionTestSupport.CreateDirectoryLink(videoLink, externalMediaRoot);

        try
        {
            var service = new WinCanDbImportService();

            var nested = service.ImportWinCanExport(sourceRoot, new Project());
            var explicitlySelected = service.ImportWinCanExport(link, new Project());
            var mediaProject = new Project();
            var mediaRecord = mediaProject.CreateNewRecord();
            mediaRecord.SetFieldValue("Haltungsname", "123", FieldSource.Xtf, userEdited: false);
            mediaProject.AddRecord(mediaRecord);
            var mediaResult = new WinCanDbImportService(
                    new UnusedM150Reader(),
                    new SuccessfulXtfImport())
                .ImportWinCanExport(mediaRoot, mediaProject);

            Assert.False(nested.Ok);
            Assert.Equal("WINCAN_DB_MISSING", nested.ErrorCode);
            Assert.True(explicitlySelected.Ok, explicitlySelected.ErrorMessage);
            Assert.Equal(1, Assert.IsType<AuswertungPro.Next.Application.Import.ImportStats>(
                explicitlySelected.Value).Errors);
            Assert.True(mediaResult.Ok, mediaResult.ErrorMessage);
            Assert.True(string.IsNullOrWhiteSpace(mediaRecord.GetFieldValue("Link")));
            Assert.Equal(original, File.ReadAllBytes(externalDb));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }

            try
            {
                if (Directory.Exists(videoLink))
                    Directory.Delete(videoLink);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }

            try
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, recursive: true);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }
        }
    }

    [Fact]
    public void ImportWinCanExport_KaputteDb3_WirdAlsEinzelfehlerZurueckgegeben()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wincan-negative-{Guid.NewGuid():N}");
        var dbDirectory = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDirectory);
        var db3Path = Path.Combine(dbDirectory, "projekt.db3");
        var original = "Das ist keine SQLite-Datenbank."u8.ToArray();
        File.WriteAllBytes(db3Path, original);
        var project = new Project();

        try
        {
            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            var stats = Assert.IsType<AuswertungPro.Next.Application.Import.ImportStats>(result.Value);
            Assert.Equal(1, stats.Errors);
            Assert.Equal(0, stats.Found);
            Assert.Equal(0, stats.Created);
            Assert.Equal(0, stats.Updated);
            Assert.Contains(stats.Messages, message =>
                message.Contains("WinCan-DB Import", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(stats.Messages, message =>
                message.Contains("Keine MDB-Datei", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(project.Data);
            Assert.Empty(project.SchaechteData);
            Assert.Equal(original, File.ReadAllBytes(db3Path));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class SuccessfulXtfImport : IXtfImportService
    {
        public bool Called { get; private set; }

        public Result<ImportStats> ImportXtfFiles(
            IEnumerable<string> xtfPaths,
            Project project,
            ImportRunContext? ctx = null)
        {
            Called = true;
            Assert.Single(xtfPaths);
            return Result<ImportStats>.Success(new ImportStats(1, 0, 0, 0, 0, []));
        }
    }

    private sealed class UnusedM150Reader : IM150MdbRowReader
    {
        public bool TryReadRows(
            string mdbPath,
            out List<Dictionary<string, string>> rows,
            out string? error)
        {
            rows = [];
            error = "Nicht erwartet.";
            return false;
        }
    }
}
