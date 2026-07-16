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
        Assert.Same(
            services.PdfFileSafety,
            services.GetService(typeof(IPdfFileSafetyChecker)));
    }

    [Fact]
    public void KompatibilitaetsFassade_kann_die_PdfPruefung_nicht_mehr_global_austauschen()
    {
        var before = PdfImportSafetyPolicy.Current;
        var use = typeof(PdfImportSafetyPolicy).GetMethod(nameof(PdfImportSafetyPolicy.Use));

        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(
            () => use.Invoke(null, [new PdfFileSafetyService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, PdfImportSafetyPolicy.Current);
    }
}
