using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class GpuModelSelectorDependencyTests
{
    [Fact]
    public void ServiceProvider_und_GpuFassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<GpuModelSelectionService>(services.GpuModels);
        Assert.Same(services.GpuModels, services.GetService(typeof(IGpuModelSelector)));
        Assert.Same(services.GpuModels, GpuModelSelector.Current);
    }
}
