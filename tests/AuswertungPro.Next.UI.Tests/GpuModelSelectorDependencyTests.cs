using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class GpuModelSelectorDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_GPU_Modellwahl_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<GpuModelSelectionService>(services.GpuModels);
        Assert.Same(services.GpuModels, services.GetService(typeof(IGpuModelSelector)));
        var field = typeof(AiPlatformSettingsResolver).GetField(
            "_gpuModels",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Same(services.GpuModels, field!.GetValue(services.AiSettings));

        var before = GpuModelSelector.Current;
        var use = typeof(GpuModelSelector).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.GpuModels]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, GpuModelSelector.Current);
    }
}
