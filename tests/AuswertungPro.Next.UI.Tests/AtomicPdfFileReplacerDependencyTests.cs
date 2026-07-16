using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AtomicPdfFileReplacerDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_PdfPruefung_und_Ersetzung_direkt()
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
        Assert.Same(
            services.PdfFileReplacement,
            services.GetService(typeof(IAtomicPdfFileReplacer)));
    }

    [Fact]
    public void KompatibilitaetsFassade_kann_die_PdfErsetzung_nicht_mehr_global_austauschen()
    {
        var before = AtomicPdfFileReplacer.Current;
        var use = typeof(AtomicPdfFileReplacer).GetMethod(nameof(AtomicPdfFileReplacer.Use));

        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(
            () => use.Invoke(null, [new AtomicPdfFileReplacementService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, AtomicPdfFileReplacer.Current);
    }
}
