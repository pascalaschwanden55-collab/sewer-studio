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
    public void ServiceProvider_Fassade_und_EinKnopfImport_verwenden_dieselbe_Instanz()
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
            KinsDbfWhitelistEnricher.Current);
        Assert.Same(
            services.KinsDbfWhitelistEnrichment,
            services.GetService(typeof(IKinsDbfWhitelistEnricher)));
    }
}
