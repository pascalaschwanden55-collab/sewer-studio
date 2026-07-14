using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.Infrastructure.Maintenance;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CodexArtifactCleanupServiceTests
{
    [Fact]
    public void Analyze_lists_only_old_generated_agent_copies()
    {
        var root = NewTestRoot();
        var artifactRoot = Path.Combine(root, CodexArtifactCleanupService.ArtifactDirectoryName);
        var cutoff = new DateTime(2026, 7, 14, 10, 0, 0, DateTimeKind.Utc);
        var old = cutoff.AddHours(-2);

        var eligible = Path.Combine(artifactRoot, "old-build");
        var recent = Path.Combine(artifactRoot, "active-build");
        var unknown = Path.Combine(artifactRoot, "unknown-content");
        var project = Path.Combine(artifactRoot, "project-content");

        try
        {
            WriteFile(Path.Combine(eligible, "bin", "app.dll"), 1_024);
            WriteFile(Path.Combine(eligible, "obj", "app.cache"), 512);
            MakeOld(eligible, old);

            WriteFile(Path.Combine(recent, "bin", "active.dll"), 100);
            WriteFile(Path.Combine(unknown, "models", "keep.bin"), 100);
            MakeOld(unknown, old);
            WriteFile(Path.Combine(project, "bin", "Projektdateien", "projekt.json"), 100);
            MakeOld(project, old);

            var report = new CodexArtifactCleanupService().Analyze(
                new CodexArtifactCleanupRequest(root, cutoff));

            var item = Assert.Single(report.Items);
            Assert.True(PathsEqual(eligible, item.Path));
            Assert.Equal(1_536, item.SizeBytes);
            Assert.Equal(2, item.FileCount);
            Assert.Contains(report.ScanWarnings, warning => warning.Contains("Kuerzlich", StringComparison.Ordinal));
            Assert.Contains(report.ScanWarnings, warning => warning.Contains("unbekanntem Inhalt", StringComparison.Ordinal));
            Assert.Contains(report.ScanWarnings, warning => warning.Contains("Projektdateien", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public void Clean_deletes_only_confirmed_old_agent_copy()
    {
        var root = NewTestRoot();
        var artifactRoot = Path.Combine(root, CodexArtifactCleanupService.ArtifactDirectoryName);
        var cutoff = DateTime.UtcNow.AddDays(-1);
        var eligible = Path.Combine(artifactRoot, "finished-build");
        var protectedCopy = Path.Combine(artifactRoot, "source-copy");

        try
        {
            WriteFile(Path.Combine(eligible, "TestResults", "result.xml"), 2_048);
            MakeOld(eligible, cutoff.AddHours(-1));
            WriteFile(Path.Combine(protectedCopy, "src", "Program.cs"), 100);
            MakeOld(protectedCopy, cutoff.AddHours(-1));

            var service = new CodexArtifactCleanupService();
            var request = new CodexArtifactCleanupRequest(root, cutoff);
            var approved = service.Analyze(request).Items.Select(item => item.Path).ToArray();
            var result = service.Clean(request, approved);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.FailedPaths));
            Assert.Equal(2_048, result.FreedBytes);
            Assert.Equal(1, result.DeletedFiles);
            Assert.Equal(1, result.DeletedDirectories);
            Assert.False(Directory.Exists(eligible));
            Assert.True(File.Exists(Path.Combine(protectedCopy, "src", "Program.cs")));
            Assert.True(Directory.Exists(artifactRoot));
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public void Clean_rechecks_activity_immediately_before_deleting()
    {
        var root = NewTestRoot();
        var artifactRoot = Path.Combine(root, CodexArtifactCleanupService.ArtifactDirectoryName);
        var cutoff = DateTime.UtcNow.AddDays(-1);
        var candidate = Path.Combine(artifactRoot, "became-active");

        try
        {
            var file = Path.Combine(candidate, "bin", "app.dll");
            WriteFile(file, 100);
            MakeOld(candidate, cutoff.AddHours(-1));

            var service = new CodexArtifactCleanupService();
            Assert.Single(service.Analyze(new CodexArtifactCleanupRequest(root, cutoff)).Items);

            File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
            var result = service.Clean(
                new CodexArtifactCleanupRequest(root, cutoff),
                [candidate]);

            Assert.False(result.Success);
            Assert.Equal(0, result.FreedBytes);
            Assert.Single(result.FailedPaths);
            Assert.True(File.Exists(file));
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public void Clean_never_deletes_old_copy_that_was_not_in_preview()
    {
        var root = NewTestRoot();
        var artifactRoot = Path.Combine(root, CodexArtifactCleanupService.ArtifactDirectoryName);
        var cutoff = DateTime.UtcNow.AddDays(-1);
        var approved = Path.Combine(artifactRoot, "approved-build");
        var notApproved = Path.Combine(artifactRoot, "newly-found-build");

        try
        {
            WriteFile(Path.Combine(approved, "bin", "approved.dll"), 100);
            WriteFile(Path.Combine(notApproved, "bin", "keep.dll"), 100);
            MakeOld(approved, cutoff.AddHours(-1));
            MakeOld(notApproved, cutoff.AddHours(-1));

            var result = new CodexArtifactCleanupService().Clean(
                new CodexArtifactCleanupRequest(root, cutoff),
                [approved]);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.FailedPaths));
            Assert.False(Directory.Exists(approved));
            Assert.True(File.Exists(Path.Combine(notApproved, "bin", "keep.dll")));
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public void Analyze_without_artifact_folder_returns_empty_report()
    {
        var root = NewTestRoot();
        try
        {
            var report = new CodexArtifactCleanupService().Analyze(
                new CodexArtifactCleanupRequest(root, DateTime.UtcNow.AddDays(-1)));

            Assert.Empty(report.Items);
            Assert.Equal(
                Path.Combine(root, CodexArtifactCleanupService.ArtifactDirectoryName),
                report.ArtifactRoot);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    private static string NewTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "codex-artifact-cleanup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    private static void MakeOld(string root, DateTime timestampUtc)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(file, timestampUtc);

        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            Directory.SetLastWriteTimeUtc(directory, timestampUtc);
        }

        Directory.SetLastWriteTimeUtc(root, timestampUtc);
    }

    private static void DeleteTestRoot(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Test-Cleanup ist best effort.
        }
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
