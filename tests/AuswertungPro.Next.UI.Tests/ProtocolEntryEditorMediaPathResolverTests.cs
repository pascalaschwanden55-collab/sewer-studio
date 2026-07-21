using System.IO;
using AuswertungPro.Next.UI.Views;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolEntryEditorMediaPathResolverTests
{
    [Fact]
    public void Explicit_project_folder_wins_and_resolves_relative_media()
    {
        using var project = new TempDirectory();
        using var settingsProject = new TempDirectory();
        var relativeVideo = $"media-{Guid.NewGuid():N}{Path.DirectorySeparatorChar}video.mp4";
        var relativeImage = $"images-{Guid.NewGuid():N}{Path.DirectorySeparatorChar}photo.jpg";
        var expectedVideo = project.CreateFile(relativeVideo);
        var expectedImage = project.CreateFile(relativeImage);
        var settingsReads = 0;
        var resolver = new ProtocolEntryEditorMediaPathResolver(
            project.RootPath,
            () =>
            {
                settingsReads++;
                return Path.Combine(settingsProject.RootPath, "projekt.json");
            });

        Assert.Equal(project.RootPath, resolver.ResolveProjectFolder());
        Assert.Equal(expectedVideo, resolver.ResolveExistingPath(relativeVideo));
        Assert.Equal([expectedImage], resolver.ResolveImagePaths([relativeImage]));
        Assert.Equal(0, settingsReads);
    }

    [Fact]
    public void Settings_project_path_is_read_lazily_and_resolves_Projektdateien_root()
    {
        using var project = new TempDirectory();
        using var laterProject = new TempDirectory();
        var projectFiles = Path.Combine(project.RootPath, "pRoJeKtDaTeIeN");
        Directory.CreateDirectory(projectFiles);
        var projectFile = Path.Combine(projectFiles, "projekt.json");
        File.WriteAllText(projectFile, "{}");
        var expectedImage = project.CreateFile("photo.jpg");
        string? currentProjectPath = null;
        var resolver = new ProtocolEntryEditorMediaPathResolver(
            projectFolder: null,
            currentProjectPath: () => currentProjectPath);

        currentProjectPath = projectFile;

        Assert.Equal(project.RootPath, resolver.ResolveProjectFolder());
        Assert.Equal(expectedImage, resolver.ResolveExistingPath("photo.jpg"));

        var laterImage = laterProject.CreateFile("later-photo.jpg");
        currentProjectPath = Path.Combine(laterProject.RootPath, "projekt.json");

        Assert.Equal(laterProject.RootPath, resolver.ResolveProjectFolder());
        Assert.Equal(laterImage, resolver.ResolveExistingPath("later-photo.jpg"));
    }

    [Fact]
    public void Missing_project_information_falls_back_to_application_base_directory()
    {
        var resolver = new ProtocolEntryEditorMediaPathResolver(
            projectFolder: null,
            currentProjectPath: () => "projekt.json");

        Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, resolver.ResolveProjectFolder());
    }

    [Fact]
    public void Existing_path_is_trimmed_while_empty_and_missing_rooted_paths_are_ignored()
    {
        using var project = new TempDirectory();
        var existing = project.CreateFile("video.mp4");
        var missing = Path.Combine(project.RootPath, "missing.mp4");
        var resolver = new ProtocolEntryEditorMediaPathResolver(project.RootPath, () => null);

        Assert.Equal(existing, resolver.ResolveExistingPath($"  {existing}  "));
        Assert.Null(resolver.ResolveExistingPath(null));
        Assert.Null(resolver.ResolveExistingPath("   "));
        Assert.Null(resolver.ResolveExistingPath(missing));
    }

    [Fact]
    public void Existing_relative_path_from_working_directory_wins_before_project_resolution()
    {
        var projectReads = 0;
        var resolver = new ProtocolEntryEditorMediaPathResolver(
            projectFolder: null,
            currentProjectPath: () =>
            {
                projectReads++;
                return "ignored.json";
            },
            fileExists: path => string.Equals(path, "cwd-photo.jpg", StringComparison.Ordinal));

        Assert.Equal("cwd-photo.jpg", resolver.ResolveExistingPath("  cwd-photo.jpg  "));
        Assert.Equal(0, projectReads);
    }

    [Fact]
    public void Image_paths_keep_order_filter_missing_and_deduplicate_ignoring_case()
    {
        using var project = new TempDirectory();
        var firstRelative = $"first-{Guid.NewGuid():N}.jpg";
        var secondRelative = $"second-{Guid.NewGuid():N}.jpg";
        var missingRelative = $"missing-{Guid.NewGuid():N}.jpg";
        var first = project.CreateFile(firstRelative);
        var second = project.CreateFile(secondRelative);
        var resolver = new ProtocolEntryEditorMediaPathResolver(project.RootPath, () => null);

        var result = resolver.ResolveImagePaths(
            [
                firstRelative,
                first.ToUpperInvariant(),
                missingRelative,
                null!,
                " ",
                secondRelative
            ]);

        Assert.Equal([first, second], result);
    }

    [Fact]
    public void Invalid_relative_path_error_is_not_hidden()
    {
        using var project = new TempDirectory();
        var resolver = new ProtocolEntryEditorMediaPathResolver(project.RootPath, () => null);

        Assert.Throws<ArgumentException>(() => resolver.ResolveExistingPath("\0broken.jpg"));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"SewerStudio-media-path-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string CreateFile(string relativePath)
        {
            var path = Path.Combine(RootPath, relativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, "test");
            return Path.GetFullPath(path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
                // Best effort: ein Test-Cleanup darf den eigentlichen Befund nicht verdecken.
            }
        }
    }
}
