using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfPrimaryDamageStructureImportTests
{
    [Fact]
    public void PdfImport_uebernimmt_Schadenszeilen_in_Findings_und_Protokoll()
    {
        var pageText = string.Join("\n", new[]
        {
            "Haltungsinspektion - 25.06.2026 - 10001-10002",
            "Haltung  10001-10002",
            "1.52 BAJB Verschobene Rohrverbindung, versetzt bei 5 Uhr",
            "",
            "6.33 A01 BBCC Harte Ablagerungen von 5 Uhr bis 7 Uhr, Start",
            "",
            "8.83 B01 BBCC Harte Ablagerungen von 5 Uhr bis 7 Uhr, Ende 4"
        });
        var service = new LegacyPdfImportService(new FixedTextExtractor(pageText));
        var project = new Project();

        var stats = service.ImportPdf("test-protokoll.pdf", project);

        Assert.Equal(0, stats.Errors);
        var record = Assert.Single(project.Data);
        Assert.Equal("10001-10002", record.GetFieldValue(FieldKeys.HoldingName));

        Assert.Equal(2, record.VsaFindings.Count);
        var pointDamage = Assert.Single(record.VsaFindings, finding => finding.KanalSchadencode == "BAJB");
        Assert.Equal(1.52, pointDamage.MeterStart);
        Assert.Null(pointDamage.MeterEnd);

        var rangeDamage = Assert.Single(record.VsaFindings, finding => finding.KanalSchadencode == "BBCC");
        Assert.Equal(6.33, rangeDamage.MeterStart);
        Assert.Equal(8.83, rangeDamage.MeterEnd);
        Assert.Equal("Harte Ablagerungen von 5 Uhr bis 7 Uhr", rangeDamage.Raw);

        Assert.NotNull(record.Protocol);
        Assert.Equal(2, record.Protocol!.Current.Entries.Count);
        var rangeEntry = Assert.Single(record.Protocol.Current.Entries, entry => entry.Code == "BBCC");
        Assert.Equal(6.33, rangeEntry.MeterStart);
        Assert.Equal(8.83, rangeEntry.MeterEnd);
        Assert.True(rangeEntry.IsStreckenschaden);
    }

    [Fact]
    public void Strukturabgleich_ersetzt_keine_bestehenden_Protokolleintraege()
    {
        var record = new HaltungRecord
        {
            Protocol = new AuswertungPro.Next.Domain.Protocol.ProtocolDocument
            {
                Original = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                {
                    Entries =
                    [
                        new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                        {
                            Code = "BAB",
                            Beschreibung = "Manuell bestaetigt",
                            MeterStart = 2.5
                        }
                    ]
                },
                Current = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                {
                    Entries =
                    [
                        new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                        {
                            Code = "BAB",
                            Beschreibung = "Manuell bestaetigt",
                            MeterStart = 2.5
                        }
                    ]
                }
            }
        };
        record.SetFieldValue(
            FieldKeys.PrimaryDamages,
            "BAJB @1.52m (Aus PDF)",
            FieldSource.Pdf,
            userEdited: false);

        PdfPrimaryDamageStructureSynchronizer.Sync(record);

        Assert.Empty(record.VsaFindings);
        var entry = Assert.Single(record.Protocol.Current.Entries);
        Assert.Equal("BAB", entry.Code);
        Assert.Equal("Manuell bestaetigt", entry.Beschreibung);
    }

    [Fact]
    public void Erneuter_PdfImport_ergaenzt_bestehende_Haltung_ohne_Strukturdaten()
    {
        const string primaryDamages = "BAJB @1.52m (Verschobene Rohrverbindung)";
        var pageText = string.Join("\n", new[]
        {
            "Haltungsinspektion - 25.06.2026 - 10001-10002",
            "Haltung  10001-10002",
            "1.52 BAJB Verschobene Rohrverbindung"
        });
        var existing = new HaltungRecord();
        existing.SetFieldValue(FieldKeys.HoldingName, "10001-10002", FieldSource.Pdf, userEdited: false);
        existing.SetFieldValue(FieldKeys.PrimaryDamages, primaryDamages, FieldSource.Pdf, userEdited: false);
        var project = new Project();
        project.Data.Add(existing);
        var service = new LegacyPdfImportService(new FixedTextExtractor(pageText));

        var stats = service.ImportPdf(
            "test-protokoll.pdf",
            project,
            fillMissingOnly: true);

        Assert.Equal(0, stats.Errors);
        Assert.Same(existing, Assert.Single(project.Data));
        Assert.Single(existing.VsaFindings);
        Assert.Equal("BAJB", existing.VsaFindings[0].KanalSchadencode);
        Assert.NotNull(existing.Protocol);
        Assert.Single(existing.Protocol!.Current.Entries);
    }

    private sealed class FixedTextExtractor(string pageText) : IPdfTextExtractor
    {
        public string FindPdfToTextPath(string? explicitPath = null)
            => throw new NotSupportedException();

        public PdfTextExtractionResult ExtractPages(
            string pdfPath,
            string? explicitPdfToTextPath = null)
            => new(new[] { pageText }, pageText);

        public void ThrowIfPageBudgetExceeded(string pdfPath, int? maxPages = null)
            => throw new NotSupportedException();
    }
}
