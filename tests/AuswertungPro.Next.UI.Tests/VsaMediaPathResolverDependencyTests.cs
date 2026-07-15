using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaMediaPathResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_und_XtfImport_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var adapter = Assert.IsType<XtfImportServiceAdapter>(services.XtfImport);
        var serviceField = typeof(XtfImportServiceAdapter).GetField(
            "_svc",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var importService = Assert.IsType<LegacyXtfImportService>(serviceField!.GetValue(adapter));
        var resolverField = typeof(LegacyXtfImportService).GetField(
            "_mediaPaths",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resolverField);
        Assert.Same(services.VsaMediaPaths, resolverField!.GetValue(importService));
        Assert.Same(
            services.VsaMediaPaths,
            services.GetService(typeof(IVsaMediaPathResolver)));
    }
}
