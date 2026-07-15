using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfTextPrefixReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Dokumenttyp_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<PdfTextPrefixReaderService>(services.PdfTextPrefixes);
        Assert.Same(services.PdfTextPrefixes, PdfDokumentTypErkennung.TextPrefixReader);
        Assert.Same(
            services.PdfTextPrefixes,
            services.GetService(typeof(IPdfTextPrefixReader)));
    }
}
