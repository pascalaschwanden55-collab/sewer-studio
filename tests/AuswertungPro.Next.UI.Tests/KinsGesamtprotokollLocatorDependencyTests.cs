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
    public void ServiceProvider_Fassade_und_EinKnopfImport_verwenden_dieselbe_Instanz()
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
            KinsGesamtprotokollLocator.Current);
        Assert.Same(
            services.KinsGesamtprotokolle,
            services.GetService(typeof(IKinsGesamtprotokollLocator)));
    }
}
