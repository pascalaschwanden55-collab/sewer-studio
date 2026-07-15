using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProcessOutputReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Kompatibilitaetsfassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<ProcessOutputReaderService>(services.ProcessOutputs);
        Assert.Same(services.ProcessOutputs, ProcessOutputReader.Current);
        Assert.Same(
            services.ProcessOutputs,
            services.GetService(typeof(IProcessOutputReader)));
    }
}
