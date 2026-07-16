using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DistributionPdfPageReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_und_verdrahtet_den_VerteilPdfLeser()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.DistributionPdfPages,
            services.GetService(typeof(IDistributionPdfPageReader)));

        var reader = Assert.IsType<DistributionPdfPageReadingService>(services.DistributionPdfPages);
        var textExtractor = typeof(DistributionPdfPageReadingService)
            .GetField("_textExtractor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(reader);
        var fileSafety = typeof(DistributionPdfPageReadingService)
            .GetField("_fileSafety", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(reader);

        Assert.Same(services.PdfTextExtraction, textExtractor);
        Assert.Same(services.PdfFileSafety, fileSafety);
    }

    [Fact]
    public void Statische_VerteilPdfFassade_ist_unveraenderbar()
    {
        var before = DistributionPdfPageReader.Current;
        var use = typeof(DistributionPdfPageReader).GetMethod(nameof(DistributionPdfPageReader.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new DistributionPdfPageReadingService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, DistributionPdfPageReader.Current);
    }
}
