using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingPdfRewriteTests
{
    [Fact]
    public void RewriteHoldingInPdfFiles_TextPdfWithHolding_RewritesInPlaceAndStaysValid()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfrw-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var pdf = Path.Combine(dir, "20250310-06-001.pdf");
        // Haltungsnummer als eigenstaendiges, von Nicht-Wort-Zeichen begrenztes Token - so steht
        // sie in echten Protokollen; der Matcher verlangt beidseitig Wortgrenzen.
        WritePdf(pdf, "Protokoll Haltung", "(06-001)", "Datum 10.03.2025");

        try
        {
            var (rewritten, _, failed) =
                HoldingFolderDistributor.RewriteHoldingInPdfFiles(new List<string> { pdf }, "06-001", "06-999");

            Assert.Equal(1, rewritten);
            Assert.Equal(0, failed);
            Assert.True(File.Exists(pdf));

            // In-place ersetzt + weiterhin ein gueltiges PDF.
            using var doc = PdfDocument.Open(pdf);
            Assert.True(doc.NumberOfPages >= 1);
        }
        finally { TryDelete(dir); }
    }

    [Fact]
    public void RewriteHoldingInPdfFiles_NoMatchInPdf_Skips()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfrw-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var pdf = Path.Combine(dir, "egal.pdf");
        WritePdf(pdf, "Nur Text ohne die gesuchte Nummer");

        try
        {
            var (rewritten, skipped, failed) =
                HoldingFolderDistributor.RewriteHoldingInPdfFiles(new List<string> { pdf }, "06-001", "06-999");

            Assert.Equal(0, rewritten);
            Assert.Equal(1, skipped);
            Assert.Equal(0, failed);
        }
        finally { TryDelete(dir); }
    }

    [Fact]
    public void RewriteHoldingInPdfFiles_MissingFile_Skips()
    {
        var (rewritten, skipped, _) = HoldingFolderDistributor.RewriteHoldingInPdfFiles(
            new List<string> { Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf") },
            "06-001", "06-999");

        Assert.Equal(0, rewritten);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void RewriteHoldingInPdfFiles_SameHolding_NoOp()
    {
        var (rewritten, skipped, failed) = HoldingFolderDistributor.RewriteHoldingInPdfFiles(
            new List<string> { "irgendwas.pdf" }, "06-001", "06-001");

        Assert.Equal(0, rewritten);
        Assert.Equal(0, skipped);
        Assert.Equal(0, failed);
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

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
    }
}
