using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using System.Reflection;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfParserHoldingNameTests
{
    [Fact]
    public void ParseFields_TableHeaderWithDatum_DetectsCorrectHaltungsname()
    {
        var text = string.Join("\n", new[]
        {
            "Kanalfernsehprotokoll / Inspektion: 1",
            "Haltungsname:                Datum :                Wetter :               Operator :",
            " 23021-22369                22.04.2014          schoen_trocken           Manuel Joschko",
            "Schacht oben: 23021",
            "Schacht unten: 22369"
        });

        var parser = new PdfParser();
        var fields = parser.ParseFields(text);

        Assert.True(fields.TryGetValue("Haltungsname", out var id));
        Assert.Equal("23021-22369", id);
    }

    [Fact]
    public void ParseFields_PhantomHoldingNameWithRepeatedDigitRuns_IsRejected()
    {
        const string phantom = "29120-000000044444449999999";
        var text = string.Join("\n", new[]
        {
            "Kanalfernsehprotokoll / Inspektion: 1",
            $"Haltungsname: {phantom}",
            "Datum: 30.06.2025",
            "Nutzungsart: Mischwasser"
        });

        var parser = new PdfParser();
        var fields = parser.ParseFields(text);

        Assert.False(fields.TryGetValue("Haltungsname", out var id), id);
    }

    [Fact]
    public void GetHaltungKeyFromChunk_PhantomHoldingNameWithRepeatedDigitRuns_IsRejected()
    {
        const string phantom = "29120-000000044444449999999";
        var text = string.Join("\n", new[]
        {
            $"Haltungsname: {phantom}",
            "Datum: 30.06.2025",
            "Nutzungsart: Mischwasser"
        });

        var key = PdfChunking.GetHaltungKeyFromChunk(text, new PdfParser());

        Assert.Null(key);
    }

    [Fact]
    public void TryExtractHoldingIdFromFileName_DatedPdfNamePrefersEmbeddedDashPair()
    {
        var id = LegacyPdfImportService.TryExtractHoldingIdFromFileName(
            @"D:\Haltungen\29120-03.27666\20250630_29120-03.27666.pdf");

        Assert.Equal("29120-03.27666", id);
    }

    [Fact]
    public void ImportPdf_FillsMissingInspectionDateFromDatedFileName()
    {
        var pdfPath = Path.Combine(
            Path.GetTempPath(),
            $"20190515_15033-15032 DP_{Guid.NewGuid():N}.pdf");

        try
        {
            WritePdf(
                pdfPath,
                "Kanalfernsehprotokoll / Inspektion: 1",
                "Bericht aus Altbestand ohne Haltungslabel im Text",
                "Strasse: Giessenstrasse");

            var project = new Project();
            var stats = new LegacyPdfImportService().ImportPdf(pdfPath, project);

            Assert.Equal(1, stats.CreatedRecords);
            Assert.Equal(0, stats.Uncertain);
            var record = Assert.Single(project.Data);
            Assert.Equal("15033-15032", record.GetFieldValue("Haltungsname"));
            Assert.Equal("15.05.2019", record.GetFieldValue("Datum_Jahr"));
        }
        finally
        {
            try
            {
                if (File.Exists(pdfPath))
                    File.Delete(pdfPath);
            }
            catch
            {
                // Best effort cleanup for Windows file handles during failed test runs.
            }
        }
    }

    [Fact]
    public void ImportPdf_FillsMissingInspectionDateFromDatedParentFolder()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pdf_import_parent_date_{Guid.NewGuid():N}");
        var dir = Path.Combine(tempRoot, "35753-35562", "20250507_35753-35562_Saniert_2025");
        var pdfPath = Path.Combine(dir, "Manuelle Arbeiten Liner End.pdf");

        try
        {
            Directory.CreateDirectory(dir);
            WritePdf(
                pdfPath,
                "Kanalfernsehprotokoll / Inspektion: 1",
                "Haltungsname: 35753-35562",
                "Nutzungsart Regenabwasser");

            var project = new Project();
            var stats = new LegacyPdfImportService().ImportPdf(pdfPath, project);

            Assert.Equal(1, stats.CreatedRecords);
            Assert.Equal(0, stats.Uncertain);
            var record = Assert.Single(project.Data);
            Assert.Equal("35753-35562", record.GetFieldValue("Haltungsname"));
            Assert.Equal("07.05.2025", record.GetFieldValue("Datum_Jahr"));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ImportPdf_UsesParentPathHolding_WhenPdfAndFileNameHaveNoHolding()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pdf_import_parent_holding_{Guid.NewGuid():N}");
        var dir = Path.Combine(tempRoot, "35722-35724", "20250515_35722-35724_Saniert_2025");
        var pdfPath = Path.Combine(dir, "Aushaertungsprotokoll H22.pdf");

        try
        {
            Directory.CreateDirectory(dir);
            WritePdf(
                pdfPath,
                "Aushaertungsprotokoll",
                "Strasse: Gotthardstr.",
                "DN: 300",
                "Nutzungsart Regenabwasser");

            var project = new Project();
            var stats = new LegacyPdfImportService().ImportPdf(pdfPath, project);

            Assert.Equal(1, stats.CreatedRecords);
            Assert.Equal(0, stats.Uncertain);
            var record = Assert.Single(project.Data);
            Assert.Equal("35722-35724", record.GetFieldValue("Haltungsname"));
            Assert.Equal("15.05.2025", record.GetFieldValue("Datum_Jahr"));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ImportPdf_FillsParentFolderDate_WhenWholeTextFallbackFindsHoldingWithoutDate()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pdf_import_fulltext_parent_date_{Guid.NewGuid():N}");
        var dir = Path.Combine(tempRoot, "35753-35562", "20250507_35753-35562_Saniert_2025");
        var pdfPath = Path.Combine(dir, "Manuelle Arbeiten Liner End.pdf");

        try
        {
            Directory.CreateDirectory(dir);
            WritePdf(
                pdfPath,
                "Schacht oben: 35753",
                "Schacht unten: 35562");

            var project = new Project();
            var stats = new LegacyPdfImportService().ImportPdf(pdfPath, project);

            Assert.Equal(1, stats.CreatedRecords);
            Assert.Equal(0, stats.Uncertain);
            var record = Assert.Single(project.Data);
            Assert.Equal("35753-35562", record.GetFieldValue("Haltungsname"));
            Assert.Equal("07.05.2025", record.GetFieldValue("Datum_Jahr"));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void WholeTextFallback_FillsParentFolderDate_WhenParsedTextHasHoldingWithoutDate()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pdf_import_direct_fallback_{Guid.NewGuid():N}");
        var dir = Path.Combine(tempRoot, "35753-35562", "20250507_35753-35562_Saniert_2025");
        var pdfPath = Path.Combine(dir, "Manuelle Arbeiten Liner End.pdf");

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(pdfPath, "");

            var project = new Project();
            var stats = new ImportStats();
            var method = typeof(LegacyPdfImportService).GetMethod(
                "TryImportFallbackHoldingFromWholeText",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            method.Invoke(null, new object?[]
            {
                "Schacht oben: 35753\nSchacht unten: 35562",
                pdfPath,
                project,
                stats,
                false,
                null
            });

            Assert.Equal(1, stats.CreatedRecords);
            Assert.Equal(0, stats.Uncertain);
            var record = Assert.Single(project.Data);
            Assert.Equal("35753-35562", record.GetFieldValue("Haltungsname"));
            Assert.Equal("07.05.2025", record.GetFieldValue("Datum_Jahr"));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void WritePdf(string path, params string[] lines)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        var y = 780m;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(40, y), font);
            y -= 18;
        }

        File.WriteAllBytes(path, builder.Build());
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup for Windows file handles during failed test runs.
        }
    }
}

