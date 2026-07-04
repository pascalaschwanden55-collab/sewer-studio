using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

public class ProjectPathResolverTests
{
    [Fact]
    public void ResolveFilePathFromProjectFolder_RejectsTraversalOutsideProject()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var outsideFolder = temp.CreateSubdir("outside");
        var outsideFile = Path.Combine(outsideFolder, "secret.jpg");
        File.WriteAllText(outsideFile, "secret");

        var raw = Path.Combine("..", "outside", "secret.jpg");

        Assert.Null(ProjectPathResolver.ResolveFilePathFromProjectFolder(raw, projectFolder));
    }

    [Fact]
    public void ResolveFilePathFromProjectFolder_AllowsExistingFileInsideProject()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var mediaDir = Path.Combine(projectFolder, "Haltungen", "06-1", "Video");
        Directory.CreateDirectory(mediaDir);
        var mediaFile = Path.Combine(mediaDir, "film.mp4");
        File.WriteAllText(mediaFile, "video");

        var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder("Haltungen/06-1/Video/film.mp4", projectFolder);

        Assert.Equal(Path.GetFullPath(mediaFile), resolved);
    }

    [Theory]
    [InlineData("Haltungen/06-1/Video/film.mp4", true)]
    [InlineData("..\\outside\\film.mp4", false)]
    [InlineData("../outside/film.mp4", false)]
    [InlineData("C:\\temp\\film.mp4", false)]
    public void IsSafeRelativeProjectPath_RejectsRootedAndTraversalPaths(string path, bool expected)
        => Assert.Equal(expected, ProjectPathResolver.IsSafeRelativeProjectPath(path));

    [Theory]
    [InlineData("..", "UNKNOWN")]
    [InlineData(".", "UNKNOWN")]
    [InlineData("...", "UNKNOWN")]
    [InlineData("  ", "UNKNOWN")]
    [InlineData(null, "UNKNOWN")]
    public void SanitizePathSegment_FaengtPunktSegmenteAb(string? input, string erwartet)
        => Assert.Equal(erwartet, ProjectPathResolver.SanitizePathSegment(input));

    [Theory]
    [InlineData("../..", "_")]
    [InlineData("..\\..", "_")]
    [InlineData("..\\..\\Windows", "_.._Windows")]
    public void SanitizePathSegment_ErzeugtNieEinTraversalSegment(string input, string erwartet)
    {
        Assert.Equal(erwartet, ProjectPathResolver.SanitizePathSegment(input));
    }

    [Theory]
    [InlineData("06.24341-35625", "06.24341-35625")]  // normaler Haltungsname bleibt
    [InlineData("Gotthardstrasse", "Gotthardstrasse")]
    [InlineData("100-200", "100-200")]
    public void SanitizePathSegment_LaesstNormaleNamenDurch(string input, string erwartet)
        => Assert.Equal(erwartet, ProjectPathResolver.SanitizePathSegment(input));

    [Fact]
    public void SanitizePathSegment_EntferntFuehrendeUndAbschliessendePunkte()
        => Assert.Equal("Haltung", ProjectPathResolver.SanitizePathSegment(".Haltung."));

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "path_resolver_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string CreateSubdir(string name)
        {
            var dir = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Cleanup-Fehler ignorieren
            }
        }
    }
}
