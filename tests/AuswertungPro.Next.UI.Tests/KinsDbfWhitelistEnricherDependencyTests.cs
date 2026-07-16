using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KinsDbfWhitelistEnricherDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_KINS_DBF_Anreicherung_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var orchestrator = services.CreateProjectImportOrchestrator();
        var enricherField = typeof(ProjectImportOrchestrator).GetField(
            "_kinsDbfWhitelistEnricher",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(enricherField);
        Assert.Same(
            services.KinsDbfWhitelistEnrichment,
            enricherField!.GetValue(orchestrator));
        Assert.Same(
            services.KinsDbfWhitelistEnrichment,
            services.GetService(typeof(IKinsDbfWhitelistEnricher)));

        var before = KinsDbfWhitelistEnricher.Current;
        var use = typeof(KinsDbfWhitelistEnricher).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.KinsDbfWhitelistEnrichment]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, KinsDbfWhitelistEnricher.Current);
    }
}
