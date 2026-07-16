using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DistributionFileTransferDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_die_Dateiuebertragung()
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
            services.GetService(typeof(IDistributionFileTransfer)));
    }

    [Fact]
    public void Statische_DateiuebertragungsFassade_ist_unveraenderbar()
    {
        var before = DistributionFileTransfer.Current;
        var use = typeof(DistributionFileTransfer).GetMethod(nameof(DistributionFileTransfer.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new DistributionFileTransferService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, DistributionFileTransfer.Current);
    }
}
