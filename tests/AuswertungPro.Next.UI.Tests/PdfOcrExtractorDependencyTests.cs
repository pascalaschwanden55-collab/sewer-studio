using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfOcrExtractorDependencyTests
{
    [Fact]
    public void ServiceProvider_und_PdfImport_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var adapter = Assert.IsType<PdfImportServiceAdapter>(services.PdfImport);
        var serviceField = typeof(PdfImportServiceAdapter).GetField(
            "_svc",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var importService = Assert.IsType<LegacyPdfImportService>(serviceField!.GetValue(adapter));
        var extractorField = typeof(LegacyPdfImportService).GetField(
            "_ocrExtractor",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(extractorField);
        Assert.Same(services.PdfOcrExtraction, extractorField!.GetValue(importService));
        Assert.Same(
            services.PdfOcrExtraction,
            services.GetService(typeof(IPdfOcrExtractor)));
    }

    [Fact]
    public void Statische_PdfOcrFassade_ist_unveraenderbar()
    {
        var before = PdfOcrExtractor.Current;
        var use = typeof(PdfOcrExtractor).GetMethod(nameof(PdfOcrExtractor.Use));
        var replacement = new PdfOcrExtractionService(new PdfTextExtractionService());

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [replacement]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, PdfOcrExtractor.Current);
    }
}
