using System;
using System.IO;
using AuswertungPro.Next.Infrastructure;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtPdfParsingTests
{
    [Fact]
    public void ParseSchachtPdfPage_ExtractsNumberAndDate_FromSchachtprotokollLine()
    {
        var text = string.Join("\n", new[]
        {
            "GEP Aufnahmen Altdorf 2025",
            "Schachtprotokoll Nr. 74467",
            "Datum 02/10/2025",
            "Visum Bachmann"
        });

        var parsed = HoldingFolderDistributor.ParseSchachtPdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("74467", parsed.ShaftNumber);
        Assert.Equal(new DateTime(2025, 10, 2), parsed.Date);
    }

    [Fact]
    public void ParseSchachtPdfPage_ExtractsNumberAndDate_FromLabeledFields()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll",
            "Schachtnummer: 12345",
            "Datum: 12.05.2025"
        });

        var parsed = HoldingFolderDistributor.ParseSchachtPdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("12345", parsed.ShaftNumber);
        Assert.Equal(new DateTime(2025, 5, 12), parsed.Date);
    }

    [Fact]
    public void ParseSchachtPdfPage_ExtractsExplicitSingleDigitShaftNumber_FromSchachtProHeader()
    {
        var text = string.Join("\n", new[]
        {
            "Projekt: Fuerlauwi Meiental Datum: 18.06.2026",
            "Schachtprotokoll Schacht Nr. 3",
            "STAMMDATEN & SKIZZE"
        });

        var parsed = HoldingFolderDistributor.ParseSchachtPdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("3", parsed.ShaftNumber);
        Assert.Equal(new DateTime(2026, 6, 18), parsed.Date);
    }

    [Fact]
    public void ParseSchachtPdfPage_ExtractsDottedShortShaftNumber_FromSchachtProHeader()
    {
        var text = string.Join("\n", new[]
        {
            "Projekt: Fuerlauwi Meiental Datum: 18.06.2026",
            "Schachtprotokoll Schacht Nr. 3.01",
            "STAMMDATEN & SKIZZE"
        });

        var parsed = HoldingFolderDistributor.ParseSchachtPdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("3.01", parsed.ShaftNumber);
        Assert.Equal(new DateTime(2026, 6, 18), parsed.Date);
    }

    [Fact]
    public void DistributeShaftFiles_SplitsGesamtauszug_ByDottedShortSchachtProHeaders()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"shaft-split-{Guid.NewGuid():N}");
        var source = Path.Combine(tempRoot, "source");
        var dest = Path.Combine(tempRoot, "dest");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(dest);
        var pdf = Path.Combine(source, "Gesamtauszug.pdf");

        try
        {
            WriteMultiPagePdf(
                pdf,
                "Schachtprotokoll Schacht Nr. 22152",
                "Schachtprotokoll Schacht Nr. 3.01",
                "Schachtprotokoll Schacht Nr. 4.01");

            var results = HoldingFolderDistributor.DistributeShaftFiles(new[] { pdf }, dest);

            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.Success, r.Message));
            Assert.Contains(results, r => r.HoldingFolder is not null && r.HoldingFolder.EndsWith($"{Path.DirectorySeparatorChar}22152", StringComparison.Ordinal));
            Assert.Contains(results, r => r.HoldingFolder is not null && r.HoldingFolder.EndsWith($"{Path.DirectorySeparatorChar}3.01", StringComparison.Ordinal));
            Assert.Contains(results, r => r.HoldingFolder is not null && r.HoldingFolder.EndsWith($"{Path.DirectorySeparatorChar}4.01", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(dest, "22152", "20260618_22152.pdf")));
            Assert.True(File.Exists(Path.Combine(dest, "3.01", "20260618_3.01.pdf")));
            Assert.True(File.Exists(Path.Combine(dest, "4.01", "20260618_4.01.pdf")));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static void WriteMultiPagePdf(string path, params string[] headers)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var header in headers)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText("Projekt: Fuerlauwi Meiental Datum: 18.06.2026", 12, new PdfPoint(40, 780), font);
            page.AddText(header, 18, new PdfPoint(40, 740), font);
            page.AddText("STAMMDATEN & SKIZZE", 12, new PdfPoint(40, 700), font);
        }

        File.WriteAllBytes(path, builder.Build());
    }
}
