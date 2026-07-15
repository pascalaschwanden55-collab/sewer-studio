using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfTextLayerRewriterDependencyTests
{
    [Fact]
    public void ServiceProvider_und_kompatible_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.PdfTextLayerRewrite, PdfTextLayerRewriter.Current);
        Assert.Same(
            services.PdfTextLayerRewrite,
            services.GetService(typeof(IPdfTextLayerRewriter)));
    }
}
