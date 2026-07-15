using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class XtfHoldingFileReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_XtfFassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.XtfHoldingFiles, XtfHelper.CurrentHoldingReader);
        Assert.Same(
            services.XtfHoldingFiles,
            services.GetService(typeof(IXtfHoldingFileReader)));
    }
}
