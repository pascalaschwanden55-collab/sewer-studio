using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class IbakExportImportServiceTests
{
    [Fact]
    public void ImportIbakExport_ReImportErsetztFindingsStattSieAnzuhaeufen()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ibak-reimport-{Guid.NewGuid():N}");
        var film = Path.Combine(root, "Film");
        Directory.CreateDirectory(film);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var data = Path.Combine(film, "Daten.txt");
        File.WriteAllText(data,
            "100-200\n" +
            "\t00:00:05    1.00 m  BCD     Schaden 1@!$ibak$!100-200$H\n" +
            "\t00:00:10    2.00 m  BCE     Schaden 2@!$ibak$!100-200$H\n",
            Encoding.GetEncoding(1252));

        try
        {
            var project = new Project();
            var service = new IbakExportImportService();
            Assert.True(service.ImportIbakExport(root, project).Ok);
            Assert.Equal(2, Assert.Single(project.Data).VsaFindings.Count);

            File.WriteAllText(data,
                "100-200\n" +
                "\t00:00:05    1.00 m  BCD     Schaden 1@!$ibak$!100-200$H\n",
                Encoding.GetEncoding(1252));
            Assert.True(service.ImportIbakExport(root, project).Ok);

            var finding = Assert.Single(Assert.Single(project.Data).VsaFindings);
            Assert.Equal("BCD", finding.KanalSchadencode);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ImportIbakExport_NeuAngelegteHaltung_BekommtFortlaufendeNr()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ibak-nr-{Guid.NewGuid():N}");
        var film = Path.Combine(root, "Film");
        Directory.CreateDirectory(film);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        File.WriteAllText(Path.Combine(film, "Daten.txt"),
            "100-200\n" +
            "\t00:00:05    1.00 m  BCD     Schaden 1@!$ibak$!100-200$H\n",
            Encoding.GetEncoding(1252));

        try
        {
            // Bestehender Record mit NR=1, damit die naechste NR deterministisch 2 ist.
            var project = new Project();
            var existing = project.CreateNewRecord();
            existing.SetFieldValue("Haltungsname", "900-901", FieldSource.Pdf, userEdited: false);
            project.AddRecord(existing);

            var result = new IbakExportImportService().ImportIbakExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            var neu = Assert.Single(project.Data, r => r.GetFieldValue("Haltungsname") == "100-200");
            // Neu angelegte IBAK-Haltung bekommt eine fortlaufende NR (einheitlich zu WinCan/KINS,
            // frueher legte IBAK per project.Data.Add ohne NR an).
            Assert.Equal("2", neu.GetFieldValue("NR"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ImportIbakExport_NormalisiertSchachtSchachtHaltungUndFuehrtMitBestehendemRecordZusammen()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ibak-ss-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Film"));
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        File.WriteAllText(
            Path.Combine(root, "Film", "Daten.txt"),
            "SS 10081-SS 8993\n" +
            "\t00:00:05    0.00 m  BCD     Rohranfang@!$ibak$!SS 10081-SS 8993$H\n" +
            "\t00:01:47    8.20 m  BCE     Rohrende@!$ibak$!SS 10081-SS 8993$H\n",
            Encoding.GetEncoding(1252));

        try
        {
            var project = new Project();
            var existing = new HaltungRecord();
            existing.SetFieldValue("Haltungsname", "10081-8993", FieldSource.Pdf, userEdited: false);
            project.Data.Add(existing);

            var result = new IbakExportImportService().ImportIbakExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            var record = Assert.Single(project.Data);
            Assert.Same(existing, record);
            Assert.Equal("10081-8993", record.GetFieldValue("Haltungsname"));
            Assert.Equal("8.2", record.GetFieldValue("Haltungslaenge_m"));
            Assert.NotNull(record.Protocol);
            Assert.Equal(2, record.Protocol!.Current.Entries.Count);
            Assert.Contains(record.VsaFindings, f => f.KanalSchadencode == "BCD");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ImportIbakExport_OrdnetHUnterstrichSchachtSchachtFotosZu()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ibak-ss-foto-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Film"));
        Directory.CreateDirectory(Path.Combine(root, "Foto"));
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        File.WriteAllText(
            Path.Combine(root, "Film", "Daten.txt"),
            "SS 10081-SS 8993\n" +
            "\t00:00:05    0.00 m  BCD     Rohranfang Foto 1@!$ibak$!SS 10081-SS 8993$H\n",
            Encoding.GetEncoding(1252));
        var foto = Path.Combine(root, "Foto", "H_SS 10081-SS 8993_001.jpg");
        File.WriteAllText(foto, "bild");

        try
        {
            var project = new Project();

            var result = new IbakExportImportService().ImportIbakExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            var record = Assert.Single(project.Data);
            Assert.Equal("10081-8993", record.GetFieldValue("Haltungsname"));
            var entry = Assert.Single(record.Protocol!.Current.Entries);
            Assert.Equal(foto, Assert.Single(entry.FotoPaths));
            Assert.Equal(foto, Assert.Single(record.VsaFindings!).FotoPath);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
