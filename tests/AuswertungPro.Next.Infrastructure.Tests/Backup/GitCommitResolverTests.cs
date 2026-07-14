using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class GitCommitResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-git-resolver-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Resolve_DetachedHead_LiestHashDirekt()
    {
        var git = Path.Combine(_root, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "abc123");

        Assert.Equal("abc123", GitCommitResolver.Resolve(_root));
    }

    [Fact]
    public void InstanceService_Resolve_DetachedHead_LiestHashDirekt()
    {
        var git = Path.Combine(_root, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "instance123");
        IGitCommitResolver resolver = new GitCommitFileResolver();

        Assert.Equal("instance123", resolver.Resolve(_root));
    }

    [Fact]
    public void Resolve_RefHead_LiestRefDatei()
    {
        var git = Path.Combine(_root, ".git");
        Directory.CreateDirectory(Path.Combine(git, "refs", "heads"));
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main");
        File.WriteAllText(Path.Combine(git, "refs", "heads", "main"), "def456");

        Assert.Equal("def456", GitCommitResolver.Resolve(_root));
    }

    [Fact]
    public void Resolve_RefHead_LiestPackedRefsWennRefDateiFehlt()
    {
        var git = Path.Combine(_root, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/feature/x");
        File.WriteAllText(Path.Combine(git, "packed-refs"), """
            # pack-refs with: peeled fully-peeled sorted
            111111 refs/heads/main
            222222 refs/heads/feature/x
            """);

        Assert.Equal("222222", GitCommitResolver.Resolve(_root));
    }

    [Fact]
    public void Resolve_FehltOderKaputt_LiefertNull()
    {
        Assert.Null(GitCommitResolver.Resolve(_root));
        Assert.Null(GitCommitResolver.Resolve(null));
    }
}
