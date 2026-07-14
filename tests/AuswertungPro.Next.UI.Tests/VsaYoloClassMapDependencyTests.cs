using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaYoloClassMapDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Yolo_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.VsaYoloClasses, VsaYoloClassMap.Current);
        Assert.Same(
            services.VsaYoloClasses,
            services.GetService(typeof(IVsaYoloClassMapStore)));
    }
}
