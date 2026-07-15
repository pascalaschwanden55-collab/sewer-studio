using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DistributionFileTransferDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Kompatibilitaetsfassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<DistributionFileTransferService>(services.DistributionFileTransfers);
        Assert.Same(
            services.DistributionFileTransfers,
            DistributionFileTransfer.Current);
        Assert.Same(
            services.DistributionFileTransfers,
            services.GetService(typeof(IDistributionFileTransfer)));
    }
}
