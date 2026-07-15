using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Protocols;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtProtocolOcrReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_SchachtImport_verwenden_dieselben_PdfDienste()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var import = Assert.IsType<SchachtProtocolImportService>(services.SchachtProtocolImport);
        var ocrField = typeof(SchachtProtocolImportService).GetField(
            "_ocrReader",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var textField = typeof(SchachtProtocolImportService).GetField(
            "_pdfTextExtractor",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsType<SchachtProtocolOcrReaderService>(services.SchachtProtocolOcr);
        Assert.Same(services.SchachtProtocolOcr, services.GetService(typeof(ISchachtProtocolOcrReader)));
        Assert.NotNull(ocrField);
        Assert.Same(services.SchachtProtocolOcr, ocrField!.GetValue(import));
        Assert.NotNull(textField);
        Assert.Same(services.PdfTextExtraction, textField!.GetValue(import));
    }
}
