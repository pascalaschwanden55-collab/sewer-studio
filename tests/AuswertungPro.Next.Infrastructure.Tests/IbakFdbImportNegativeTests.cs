using System.Text;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class IbakFdbImportNegativeTests
{
    [Fact]
    public void ImportIbakExport_KaputteFdb_BleibtUnveraendertUndDatenTxtWirdWeiterImportiert()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ibak-fdb-negative-{Guid.NewGuid():N}");
        var filmDirectory = Path.Combine(root, "Film");
        var dataDirectory = Path.Combine(root, "Data");
        Directory.CreateDirectory(filmDirectory);
        Directory.CreateDirectory(dataDirectory);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        File.WriteAllText(
            Path.Combine(filmDirectory, "Daten.txt"),
            "100-200\n"
            + "\t00:00:05    1.00 m  BCD     Schaden@!$ibak$!100-200$H\n",
            Encoding.GetEncoding(1252));

        var fdbPath = Path.Combine(dataDirectory, "Arizona.fdb");
        var original = "Keine Firebird-Datenbank; Kundenoriginal."u8.ToArray();
        File.WriteAllBytes(fdbPath, original);
        // Erzwingt einen schnellen, lokalen Client-Ladefehler statt eines moeglichen
        // Verbindungsversuchs zu einem installierten Firebird-Dienst.
        File.WriteAllBytes(Path.Combine(root, "fbclient.dll"), [0x00, 0x01, 0x02]);
        var project = new Project();

        try
        {
            var result = new IbakExportImportService().ImportIbakExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            var stats = Assert.IsType<AuswertungPro.Next.Application.Import.ImportStats>(result.Value);
            Assert.Equal(0, stats.Errors);
            Assert.Equal(1, stats.Found);
            Assert.Contains(stats.Messages, message =>
                message.Contains("IBAK FDB: Zugriff fehlgeschlagen", StringComparison.OrdinalIgnoreCase)
                && message.Contains("Fallback", StringComparison.OrdinalIgnoreCase));

            var record = Assert.Single(project.Data);
            Assert.Equal("100-200", record.GetFieldValue("Haltungsname"));
            Assert.Contains(record.VsaFindings, finding => finding.KanalSchadencode == "BCD");
            Assert.Equal(original, File.ReadAllBytes(fdbPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
