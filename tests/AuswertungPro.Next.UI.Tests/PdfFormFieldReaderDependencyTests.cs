using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfFormFieldReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_den_PdfFormularfeldLeser()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<PdfFormFieldReaderService>(services.PdfFormFields);
        Assert.Same(services.PdfFormFields, services.GetService(typeof(IPdfFormFieldReader)));
    }

    [Fact]
    public void Statische_PdfFormularfeldFassade_ist_unveraenderbar()
    {
        var before = PdfFormFieldExtractor.Current;
        var use = typeof(PdfFormFieldExtractor).GetMethod(nameof(PdfFormFieldExtractor.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new PdfFormFieldReaderService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, PdfFormFieldExtractor.Current);
    }
}
