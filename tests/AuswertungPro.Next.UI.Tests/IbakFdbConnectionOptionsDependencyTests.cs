using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class IbakFdbConnectionOptionsDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_IBAK_Optionen_direkt_und_Fassade_bleibt_unveraenderlich()
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
        Assert.NotNull(field);
        Assert.Same(services.IbakConnections, field!.GetValue(services.IbakImport));

        var before = IbakFdbConnectionOptions.Current;
        var use = typeof(IbakFdbConnectionOptions).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.IbakConnections]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, IbakFdbConnectionOptions.Current);
    }
}
