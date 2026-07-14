using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class RepositoryRootFileLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "RepositoryRootFileLocatorTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Locate_findet_die_Solution_auch_von_einer_Datei_und_bleibt_sonst_leer()
    {
        var repositoryRoot = Path.Combine(_root, "repo");
        var nestedDirectory = Path.Combine(repositoryRoot, "src", "UI", "bin");
        var startFile = Path.Combine(nestedDirectory, "SewerStudio.dll");
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(Path.Combine(repositoryRoot, "AuswertungPro.sln"), "test");
        File.WriteAllText(startFile, "test");

        IRepositoryRootLocator locator = new RepositoryRootFileLocator();

        Assert.Equal(repositoryRoot, locator.Locate(startFile));
        Assert.Null(locator.Locate(Path.Combine(_root, "ohne-solution")));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }
}
