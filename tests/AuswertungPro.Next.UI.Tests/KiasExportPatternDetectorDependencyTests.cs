using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KiasExportPatternDetectorDependencyTests
{
    [Fact]
    public void ServiceProvider_Fassade_und_KanalErkennung_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        var detectorField = typeof(KanalExportDetectionService).GetField(
            "_kiasExportPatternDetector",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(detectorField);
        Assert.Same(
            services.KiasExportPatterns,
            detectorField!.GetValue(services.KanalExportDetection));
        Assert.Same(services.KiasExportPatterns, KiasExportPattern.Current);
        Assert.Same(
            services.KiasExportPatterns,
            services.GetService(typeof(IKiasExportPatternDetector)));
    }
}
