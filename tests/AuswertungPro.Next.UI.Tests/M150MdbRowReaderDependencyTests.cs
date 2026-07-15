using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class M150MdbRowReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_statische_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.M150MdbRows, M150MdbRowReader.Current);
        Assert.Same(
            services.M150MdbRows,
            services.GetService(typeof(IM150MdbRowReader)));
    }
}
