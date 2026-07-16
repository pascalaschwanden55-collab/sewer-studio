using System.Reflection;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class RepositoryRootLocatorDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_die_Projektordnersuche_ohne_veraenderbaren_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.RepositoryRootLocator,
            services.GetService(typeof(IRepositoryRootLocator)));
        var use = typeof(RepoRootLocator).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(
            () => use.Invoke(null, new object?[] { RepoRootLocator.Current }));
        Assert.IsType<NotSupportedException>(error.InnerException);
    }
}
