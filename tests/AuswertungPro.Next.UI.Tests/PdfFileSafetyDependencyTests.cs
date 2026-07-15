using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfFileSafetyDependencyTests
{
    [Fact]
    public void ServiceProvider_Textauslese_und_Ocr_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        var textSafety = typeof(PdfTextExtractionService).GetField(
            "_fileSafety",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var ocrSafety = typeof(PdfOcrExtractionService).GetField(
            "_fileSafety",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(textSafety);
        Assert.NotNull(ocrSafety);
        Assert.Same(services.PdfFileSafety, textSafety!.GetValue(services.PdfTextExtraction));
        Assert.Same(services.PdfFileSafety, ocrSafety!.GetValue(services.PdfOcrExtraction));
        Assert.Same(services.PdfFileSafety, PdfImportSafetyPolicy.Current);
        Assert.Same(
            services.PdfFileSafety,
            services.GetService(typeof(IPdfFileSafetyChecker)));
    }
}
