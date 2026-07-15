using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KinsDvdTextEnricherDependencyTests
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
            "_kinsDvdTextEnricher",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(enricherField);
        Assert.Same(
            services.KinsDvdTextEnrichment,
            enricherField!.GetValue(orchestrator));
        Assert.Same(
            services.KinsDvdTextEnrichment,
            KinsDvdTextEnricher.Current);
        Assert.Same(
            services.KinsDvdTextEnrichment,
            services.GetService(typeof(IKinsDvdTextEnricher)));
    }
}
