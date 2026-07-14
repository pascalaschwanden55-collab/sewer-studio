using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDefectPreviewDependencyTests
{
    [Fact]
    public void ServiceProvider_und_statische_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsAssignableFrom<ICodingDefectPreviewRenderer>(services.CodingDefectPreviews);
        Assert.Same(
            services.CodingDefectPreviews,
            CodingDefectPreviewService.CompatibilityService);
        Assert.Same(
            services.CodingDefectPreviews,
            services.GetService(typeof(ICodingDefectPreviewRenderer)));
    }
}
