using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfTextLayerRewriterDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_die_PdfTextkorrektur()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.PdfTextLayerRewrite,
            services.GetService(typeof(IPdfTextLayerRewriter)));
    }

    [Fact]
    public void Statische_PdfTextkorrekturFassade_ist_unveraenderbar()
    {
        var before = PdfTextLayerRewriter.Current;
        var use = typeof(PdfTextLayerRewriter).GetMethod(nameof(PdfTextLayerRewriter.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new PdfTextLayerRewriteService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, PdfTextLayerRewriter.Current);
    }
}
