using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfTextExtractorDependencyTests
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
            "_textExtractor",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(extractorField);
        Assert.Same(services.PdfTextExtraction, extractorField!.GetValue(importService));
        Assert.Same(services.PdfTextExtraction, PdfTextExtractor.Current);
        Assert.Same(
            services.PdfTextExtraction,
            services.GetService(typeof(IPdfTextExtractor)));
    }
}
