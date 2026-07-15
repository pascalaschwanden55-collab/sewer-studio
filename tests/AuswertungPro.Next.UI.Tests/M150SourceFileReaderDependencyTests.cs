using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class M150SourceFileReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_M150Fassade_verwenden_dieselbe_QuelldateiInstanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.M150SourceFiles, M150SourceFileReader.Current);
        Assert.Same(
            services.M150SourceFiles,
            services.GetService(typeof(IM150SourceFileReader)));
    }
}
