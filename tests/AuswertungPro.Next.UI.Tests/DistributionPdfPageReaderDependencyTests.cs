using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DistributionPdfPageReaderDependencyTests
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

        Assert.Same(services.DistributionPdfPages, DistributionPdfPageReader.Current);
        Assert.Same(
            services.DistributionPdfPages,
            services.GetService(typeof(IDistributionPdfPageReader)));
    }
}
