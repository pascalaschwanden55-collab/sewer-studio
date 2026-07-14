using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class RepositoryRootLocatorDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Repo_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.RepositoryRootLocator, RepoRootLocator.Current);
        Assert.Same(
            services.RepositoryRootLocator,
            services.GetService(typeof(IRepositoryRootLocator)));
    }
}
