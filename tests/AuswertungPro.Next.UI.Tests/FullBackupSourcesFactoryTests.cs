using System.Collections;
using System.IO;
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

    [Fact]
    public void Instanzdienst_filtert_Umgebung_und_verwendet_injizierte_Systempfade()
    {
        var variables = new Hashtable
        {
            ["PATH"] = "ignorieren",
            ["SEWERSTUDIO_KNOWLEDGE_ROOT"] = @"C:\Knowledge",
            ["SEWER_TOKEN"] = "token"
        };
        var locator = new RepositoryRootLocatorFake(@"C:\Repo");
        var provider = new FullBackupSourcesProvider(
            locator,
            getKnowledgeRoot: () => @"C:\Knowledge",
            localSewerStudioDir: @"C:\Local\SewerStudio",
            getFolderPath: folder => folder switch
            {
                Environment.SpecialFolder.ApplicationData => @"C:\Roaming",
                Environment.SpecialFolder.DesktopDirectory => @"C:\Desktop",
                _ => throw new ArgumentOutOfRangeException(nameof(folder))
            },
            getEnvironmentVariables: () => variables,
            baseDirectory: @"C:\App",
            appVersion: "4.5-test");

        var sources = provider.Resolve(new AppSettings
        {
            ProjectsRootDirectory = @"D:\Projekte",
            FullBackupIncludeProjectVideos = true
        });

        Assert.Equal(@"C:\Repo", sources.RepoRoot);
        Assert.Equal(@"C:\App", locator.StartPath);
        Assert.Equal(@"C:\Knowledge", sources.KnowledgeRoot);
        Assert.Equal(@"C:\Local\SewerStudio", sources.LocalSewerStudioDir);
        Assert.Equal(Path.Combine(@"C:\Roaming", "SewerStudio"), sources.RoamingSewerStudioDir);
        Assert.Equal(Path.Combine(@"C:\Roaming", "AuswertungPro"), sources.RoamingAuswertungProDir);
        Assert.Equal(@"C:\Desktop", sources.DesktopDir);
        Assert.Equal("4.5-test", sources.AppVersion);
        Assert.Equal([@"D:\Projekte"], sources.ProjectRoots);
        Assert.True(sources.IncludeProjectVideos);
        Assert.Equal(2, sources.EnvironmentVariables.Count);
        Assert.False(sources.EnvironmentVariables.ContainsKey("PATH"));
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
