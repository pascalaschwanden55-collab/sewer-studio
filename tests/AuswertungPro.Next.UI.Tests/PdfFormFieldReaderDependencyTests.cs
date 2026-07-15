using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfFormFieldReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_FormularFassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<PdfFormFieldReaderService>(services.PdfFormFields);
        Assert.Same(services.PdfFormFields, services.GetService(typeof(IPdfFormFieldReader)));
        Assert.Same(services.PdfFormFields, PdfFormFieldExtractor.Current);
    }
}
