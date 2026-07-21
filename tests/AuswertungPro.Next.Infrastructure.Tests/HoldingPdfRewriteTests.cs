using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
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
            Assert.True(File.Exists(pdf + ".bak"));

            // In-place ersetzt + weiterhin ein gueltiges PDF.
            using var doc = PdfDocument.Open(pdf);
            Assert.True(doc.NumberOfPages >= 1);
            Assert.Contains("06-999", doc.GetPage(1).Text, StringComparison.Ordinal);

            // Die vorherige Fassung bleibt als lesbare Sicherung erhalten.
            using var backup = PdfDocument.Open(pdf + ".bak");
            Assert.True(backup.NumberOfPages >= 1);
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

    [Fact]
    public void AppendPdfFile_ErsetztAtomarUndLoeschtZusatzErstNachErfolg()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfappend-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "ziel.pdf");
        var additional = Path.Combine(dir, "zusatz.pdf");
        WritePdf(target, "Seite eins");
        WritePdf(additional, "Seite zwei");

        try
        {
            HoldingFolderDistributor.AppendPdfFile(target, additional, removeAdditionalWhenMoved: true);

            using var merged = PdfDocument.Open(target);
            Assert.Equal(2, merged.NumberOfPages);
            Assert.False(File.Exists(additional));
            Assert.True(File.Exists(target + ".bak"));

            using var backup = PdfDocument.Open(target + ".bak");
            Assert.Equal(1, backup.NumberOfPages);
        }
        finally { TryDelete(dir); }
    }

    [Fact]
    public void RewriteHoldingInPdfFiles_UngueltigePdf_ZaehltAlsFehler()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfrw-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var pdf = Path.Combine(dir, "kaputt.pdf");
        File.WriteAllText(pdf, "keine PDF");
        var originalBytes = File.ReadAllBytes(pdf);

        try
        {
            var (rewritten, skipped, failed) = HoldingFolderDistributor.RewriteHoldingInPdfFiles(
                new List<string> { pdf },
                "06-001",
                "06-999");

            Assert.Equal(0, rewritten);
            Assert.Equal(0, skipped);
            Assert.Equal(1, failed);
            Assert.Equal(originalBytes, File.ReadAllBytes(pdf));
            Assert.False(File.Exists(pdf + ".bak"));
        }
        finally { TryDelete(dir); }
    }

    [Fact]
    public void PdfTextLayerRewriteService_RewriteIdentifierInPlace_StelltBatchVertragBereit()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfrw-service-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var pdf = Path.Combine(dir, "haltung.pdf");
        WritePdf(pdf, "Haltung (06-001)");

        try
        {
            var service = new PdfTextLayerRewriteService();

            var result = service.RewriteIdentifierInPlace(
                new List<string> { pdf },
                "06-001",
                "06-999");

            Assert.Equal(1, result.Rewritten);
            Assert.Equal(0, result.Skipped);
            Assert.Equal(0, result.Failed);
            Assert.True(File.Exists(pdf + ".bak"));
        }
        finally { TryDelete(dir); }
    }

    [Fact]
    public void RewriteIdentifierInPlace_DefektePdfStopptGueltigePdfNicht()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfrw-continue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var invalidPdf = Path.Combine(dir, "kaputt.pdf");
        var validPdf = Path.Combine(dir, "gueltig.pdf");
        File.WriteAllText(invalidPdf, "keine PDF");
        WritePdf(validPdf, "Haltung (06-001)");

        try
        {
            var service = new PdfTextLayerRewriteService();

            var result = service.RewriteIdentifierInPlace(
                new List<string> { invalidPdf, validPdf },
                "06-001",
                "06-999");

            Assert.Equal(1, result.Rewritten);
            Assert.Equal(0, result.Skipped);
            Assert.Equal(1, result.Failed);
            var failure = Assert.Single(result.Failures);
            Assert.Equal(invalidPdf, failure.PdfPath, ignoreCase: true);
            Assert.NotEmpty(failure.Message);
            using var rewritten = PdfDocument.Open(validPdf);
            Assert.Contains("06-999", rewritten.GetPage(1).Text, StringComparison.Ordinal);
        }
        finally { TryDelete(dir); }
    }

    [Fact]
    public void RewriteIdentifierInPlace_ErsetzungsfehlerLaesstOriginalUndRaeumtTempDateiAuf()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfrw-replace-fail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var pdf = Path.Combine(dir, "haltung.pdf");
        WritePdf(pdf, "Haltung (06-001)");
        var originalBytes = File.ReadAllBytes(pdf);
        var replacer = new ThrowingAtomicPdfFileReplacer();

        try
        {
            var service = new PdfTextLayerRewriteService(replacer);

            var result = service.RewriteIdentifierInPlace(
                new List<string> { pdf },
                "06-001",
                "06-999");

            Assert.Equal(0, result.Rewritten);
            Assert.Equal(0, result.Skipped);
            Assert.Equal(1, result.Failed);
            var failure = Assert.Single(result.Failures);
            Assert.Equal(pdf, failure.PdfPath, ignoreCase: true);
            Assert.Contains("Testfehler beim Ersetzen", failure.Message, StringComparison.Ordinal);
            Assert.Equal(originalBytes, File.ReadAllBytes(pdf));
            Assert.False(File.Exists(pdf + ".bak"));
            Assert.NotNull(replacer.GeneratedPdfPath);
            Assert.False(File.Exists(replacer.GeneratedPdfPath));
        }
        finally { TryDelete(dir); }
    }

    [Fact]
    public void AppendPdfFile_UngueltigerZusatz_LaesstBeideOriginaleUnveraendert()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfappend-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "ziel.pdf");
        var additional = Path.Combine(dir, "zusatz.pdf");
        WritePdf(target, "Original");
        File.WriteAllText(additional, "keine PDF");
        var originalBytes = File.ReadAllBytes(target);

        try
        {
            Assert.ThrowsAny<Exception>(() =>
                HoldingFolderDistributor.AppendPdfFile(target, additional, removeAdditionalWhenMoved: true));

            Assert.Equal(originalBytes, File.ReadAllBytes(target));
            Assert.True(File.Exists(additional));
        }
        finally { TryDelete(dir); }
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

    private sealed class ThrowingAtomicPdfFileReplacer : IAtomicPdfFileReplacer
    {
        public string? GeneratedPdfPath { get; private set; }

        public void ReplaceValidated(string generatedPdfPath, string targetPdfPath)
        {
            GeneratedPdfPath = generatedPdfPath;
            throw new IOException("Testfehler beim Ersetzen.");
        }
    }
}
