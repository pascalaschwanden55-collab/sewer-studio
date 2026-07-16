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
    public void ServiceProvider_verdrahtet_KINS_Textanreicherung_direkt_und_Fassade_bleibt_unveraenderlich()
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
            services.GetService(typeof(IKinsDvdTextEnricher)));

        var before = KinsDvdTextEnricher.Current;
        var use = typeof(KinsDvdTextEnricher).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.KinsDvdTextEnrichment]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, KinsDvdTextEnricher.Current);
    }
}
