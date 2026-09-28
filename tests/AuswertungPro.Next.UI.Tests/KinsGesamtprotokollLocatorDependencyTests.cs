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
        // Die Suche nach dem KINS-Gesamtprotokoll gehoert zur Medienphase des Imports.
        var phaseField = typeof(ProjectImportOrchestrator).GetField(
            "_mediaPhase",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(phaseField);
        var phase = phaseField!.GetValue(orchestrator);
        Assert.NotNull(phase);
        var locatorField = phase!.GetType().GetField(
            "_kinsGesamtprotokollLocator",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(locatorField);
        Assert.Same(
            services.KinsGesamtprotokolle,
            locatorField!.GetValue(phase));
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
