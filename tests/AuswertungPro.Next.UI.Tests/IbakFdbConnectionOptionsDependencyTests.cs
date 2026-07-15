using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class IbakFdbConnectionOptionsDependencyTests
{
    [Fact]
    public void ServiceProvider_Import_und_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var field = typeof(IbakExportImportService).GetField(
            "_connectionOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsType<IbakFdbConnectionOptionsService>(services.IbakConnections);
        Assert.Same(
            services.IbakConnections,
            services.GetService(typeof(IIbakFdbConnectionOptions)));
        Assert.Same(services.IbakConnections, IbakFdbConnectionOptions.Current);
        Assert.NotNull(field);
        Assert.Same(services.IbakConnections, field!.GetValue(services.IbakImport));
    }
}
