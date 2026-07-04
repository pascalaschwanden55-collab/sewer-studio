using System.IO;
using AuswertungPro.Next.UI.Mapping;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KatasterXtfPathResolverTests
{
    [Fact]
    public void Resolve_keeps_existing_explicit_file()
    {
        using var dir = new TempDirectory();
        var explicitFile = Path.Combine(dir.Path, "custom.xtf");
        var directoryFile = Path.Combine(dir.Path, "Abwasserkataster_Uri_korrigiert.xtf");
        File.WriteAllText(explicitFile, "<TRANSFER />");
        File.WriteAllText(directoryFile, "<TRANSFER />");

        var resolved = KatasterXtfPathResolver.Resolve(explicitFile, dir.Path);

        Assert.Equal(explicitFile, resolved);
    }

    [Fact]
    public void Resolve_uses_preferred_file_from_directory_when_explicit_file_is_missing()
    {
        using var dir = new TempDirectory();
        var expected = Path.Combine(dir.Path, "Abwasserkataster_Uri_korrigiert.xtf");
        File.WriteAllText(expected, "<TRANSFER />");

        var resolved = KatasterXtfPathResolver.Resolve(
            @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf",
            dir.Path);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Resolve_accepts_directory_in_explicit_path_field()
    {
        using var dir = new TempDirectory();
        var expected = Path.Combine(dir.Path, "Abwasserkataster_Uri.xtf");
        File.WriteAllText(expected, "<TRANSFER />");

        var resolved = KatasterXtfPathResolver.Resolve(dir.Path, null);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Resolve_falls_back_to_largest_xtf_when_known_file_names_are_missing()
    {
        using var dir = new TempDirectory();
        var small = Path.Combine(dir.Path, "small.xtf");
        var large = Path.Combine(dir.Path, "large.xtf");
        File.WriteAllText(small, "1");
        File.WriteAllText(large, "12345");

        var resolved = KatasterXtfPathResolver.Resolve(null, dir.Path);

        Assert.Equal(large, resolved);
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
