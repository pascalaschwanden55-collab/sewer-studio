using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class RepoRootLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-repo-root-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Locate_FindetAuswertungProSlnAufwaerts()
    {
        var repo = Path.Combine(_root, "repo");
        var nested = Path.Combine(repo, "src", "AuswertungPro.Next.UI", "bin");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(repo, "AuswertungPro.sln"), "");

        Assert.Equal(repo, RepoRootLocator.Locate(nested));
    }

    [Fact]
    public void Locate_StartpfadDarfDateiSein()
    {
        var repo = Path.Combine(_root, "repo");
        var nested = Path.Combine(repo, "src");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(repo, "AuswertungPro.sln"), "");
        var file = Path.Combine(nested, "x.dll");
        File.WriteAllText(file, "");

        Assert.Equal(repo, RepoRootLocator.Locate(file));
    }

    [Fact]
    public void Locate_OhneSolution_LiefertNull()
    {
        var nested = Path.Combine(_root, "a", "b");
        Directory.CreateDirectory(nested);

        Assert.Null(RepoRootLocator.Locate(nested));
    }
}
