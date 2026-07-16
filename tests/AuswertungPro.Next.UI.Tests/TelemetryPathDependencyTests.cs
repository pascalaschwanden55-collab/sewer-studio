using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TelemetryPathDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_Telemetriepfade_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<TelemetryFilePathResolver>(services.TelemetryPaths);
        Assert.Same(
            services.TelemetryPaths,
            services.GetService(typeof(ITelemetryPathResolver)));

        AssertUsesPaths(services.SidecarTelemetry, services.TelemetryPaths);
        AssertUsesPaths(services.PipelineTrace, services.TelemetryPaths);
        AssertUsesPaths(services.VsaShadowTelemetry, services.TelemetryPaths);

        var before = TelemetryPathResolver.Current;
        var use = typeof(TelemetryPathResolver).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.TelemetryPaths]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, TelemetryPathResolver.Current);
    }

    private static void AssertUsesPaths(object writer, ITelemetryPathResolver expected)
    {
        var field = writer.GetType().GetField(
            "_paths",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(expected, field!.GetValue(writer));
    }
}
