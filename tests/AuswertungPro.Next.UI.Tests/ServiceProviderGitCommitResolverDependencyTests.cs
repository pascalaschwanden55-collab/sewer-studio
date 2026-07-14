using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderGitCommitResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_Stellt_GitCommitResolver_Als_Vertrag_Bereit()
    {
        var property = typeof(ServiceProvider)
            .GetProperty(nameof(ServiceProvider.GitCommit));

        Assert.NotNull(property);
        Assert.Equal(typeof(IGitCommitResolver), property!.PropertyType);
    }
}
