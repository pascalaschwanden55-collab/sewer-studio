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
    public void ServiceProvider_verdrahtet_KIAS_Erkennung_direkt_und_Fassade_bleibt_unveraenderlich()
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
        Assert.Same(
            services.KiasExportPatterns,
            services.GetService(typeof(IKiasExportPatternDetector)));

        var before = KiasExportPattern.Current;
        var use = typeof(KiasExportPattern).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.KiasExportPatterns]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, KiasExportPattern.Current);
    }
}
