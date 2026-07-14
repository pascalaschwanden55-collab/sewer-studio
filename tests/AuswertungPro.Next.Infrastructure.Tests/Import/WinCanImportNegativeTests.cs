using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class WinCanImportNegativeTests
{
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
}
