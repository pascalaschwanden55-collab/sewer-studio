using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class XtfStammdatenSourceReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_StammdatenFassade_verwenden_dieselbe_XtfQuelle()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.XtfStammdatenSources,
            XtfStammdatenExtractor.CurrentSourceReader);
        Assert.Same(
            services.XtfStammdatenSources,
            services.GetService(typeof(IXtfStammdatenSourceReader)));
    }
}
