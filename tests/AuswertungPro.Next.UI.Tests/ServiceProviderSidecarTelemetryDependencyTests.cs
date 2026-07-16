using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderSidecarTelemetryDependencyTests
{
    [Fact]
    public void ServiceProvider_stellt_Sidecar_Telemetrie_zentral_bereit()
    {
        var property = typeof(ServiceProvider)
            .GetProperty(nameof(ServiceProvider.SidecarTelemetry));

        Assert.NotNull(property);
        Assert.Equal(typeof(ISidecarTelemetryWriter), property.PropertyType);
        Assert.False(property.CanWrite);
    }

    [Fact]
    public void VideoPipeline_erhaelt_die_registrierte_Sidecar_Telemetrie_direkt()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings(),
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var field = typeof(VideoAnalysisPipelineFactory).GetField(
            "_sidecarTelemetry",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(services.SidecarTelemetry, field.GetValue(services.VideoAnalysisPipelines));
        Assert.Same(
            services.SidecarTelemetry,
            services.GetService(typeof(ISidecarTelemetryWriter)));
    }

    [Fact]
    public void KompatibilitaetsFassade_kann_den_Schreiber_nicht_mehr_global_austauschen()
    {
        var before = SidecarTelemetryWriter.Current;
        var use = typeof(SidecarTelemetryWriter).GetMethod(nameof(SidecarTelemetryWriter.Use));

        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(
            () => use.Invoke(null, [new RecordingTelemetryWriter()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, SidecarTelemetryWriter.Current);
    }

    private sealed class RecordingTelemetryWriter : ISidecarTelemetryWriter
    {
        public Task WriteAsync(SidecarTelemetryEntry entry) => Task.CompletedTask;

        public string? ResolvePath() => null;
    }
}
