using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FullBackupSourcesFactoryTests
{
    [Fact]
    public void ErmittleAktuelleQuellen_verwendet_die_injizierte_Repo_Suche()
    {
        var locator = new RepositoryRootLocatorFake("C:\\SewerStudio-Repo");

        var sources = FullBackupSourcesFactory.ErmittleAktuelleQuellen(
            new AppSettings(),
            locator);

        Assert.Equal("C:\\SewerStudio-Repo", sources.RepoRoot);
        Assert.Equal(AppContext.BaseDirectory, locator.StartPath);
    }

    private sealed class RepositoryRootLocatorFake(string result) : IRepositoryRootLocator
    {
        public string? StartPath { get; private set; }

        public string? Locate(string? startPath)
        {
            StartPath = startPath;
            return result;
        }
    }
}
