using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KnowledgeBasePathServiceDependencyTests
{
    [Fact]
    public void ServiceProvider_und_kompatible_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.KnowledgePaths, KnowledgeBasePaths.Current);
        Assert.Same(
            services.KnowledgePaths,
            services.GetService(typeof(IKnowledgeBasePathService)));
    }
}
