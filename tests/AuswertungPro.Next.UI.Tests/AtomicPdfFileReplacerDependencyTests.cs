using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AtomicPdfFileReplacerDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var safetyField = typeof(AtomicPdfFileReplacementService).GetField(
            "_fileSafety",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(safetyField);
        Assert.Same(
            services.PdfFileSafety,
            safetyField!.GetValue(services.PdfFileReplacement));
        Assert.Same(services.PdfFileReplacement, AtomicPdfFileReplacer.Current);
        Assert.Same(
            services.PdfFileReplacement,
            services.GetService(typeof(IAtomicPdfFileReplacer)));
    }
}
