using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KinsGesamtprotokollLocatorDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_KINS_Gesamtprotokollsuche_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var orchestrator = services.CreateProjectImportOrchestrator();
        var locatorField = typeof(ProjectImportOrchestrator).GetField(
            "_kinsGesamtprotokollLocator",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(locatorField);
        Assert.Same(
            services.KinsGesamtprotokolle,
            locatorField!.GetValue(orchestrator));
        Assert.Same(
            services.KinsGesamtprotokolle,
            services.GetService(typeof(IKinsGesamtprotokollLocator)));

        var before = KinsGesamtprotokollLocator.Current;
        var use = typeof(KinsGesamtprotokollLocator).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.KinsGesamtprotokolle]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, KinsGesamtprotokollLocator.Current);
    }
}
