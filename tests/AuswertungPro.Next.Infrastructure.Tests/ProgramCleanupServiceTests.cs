using AuswertungPro.Next.Infrastructure.Maintenance;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProgramCleanupServiceTests
{
    [Fact]
    public void Analyze_and_clean_remove_only_whitelisted_program_data()
    {
        var testRoot = NewTestRoot();
        var programRoot = Path.Combine(testRoot, "program");
        var systemTemp = Path.Combine(testRoot, "windows-temp");
        var currentAppBase = Path.Combine(
            programRoot,
            "src",
            "App",
            "bin",
            "Debug",
            "net10.0-windows");
        var protectedProject = Path.Combine(programRoot, "src", "ProtectedProject");

        try
        {
            WriteFile(Path.Combine(programRoot, ".tmp", "large.bin"), 2_048);
            WriteFile(Path.Combine(programRoot, ".tmp-project", "Projektdateien", "projekt.json"), 128);
            WriteFile(Path.Combine(programRoot, "src", "App", "obj", "cache.bin"), 1_024);
            WriteFile(Path.Combine(programRoot, "src", "Other", "bin", "old.dll"), 512);
            WriteFile(Path.Combine(programRoot, "src", "App", "bin", "Release", "old.dll"), 256);
            WriteFile(Path.Combine(currentAppBase, "SewerStudio.exe"), 128);

            WriteFile(Path.Combine(programRoot, "src", "App", "important.tmp"), 64);
            WriteFile(Path.Combine(programRoot, "artifacts", "release", "bin", "keep.dll"), 64);
            WriteFile(Path.Combine(programRoot, "sidecar", "models", "__pycache__", "keep.pyc"), 64);
            WriteFile(Path.Combine(protectedProject, "project.json"), 64);
            WriteFile(Path.Combine(protectedProject, "bin", "keep.dll"), 64);
            WriteFile(Path.Combine(programRoot, "CustomerProject", "bin", "keep.dll"), 64);
            Directory.CreateDirectory(systemTemp);

            var request = new ProgramCleanupRequest(
                programRoot,
                systemTemp,
                currentAppBase,
                [protectedProject],
                DateTime.UtcNow.AddDays(-1));
            var service = new ProgramCleanupService();

            var report = service.Analyze(request);

            Assert.Contains(report.Items, item => PathsEqual(item.Path, Path.Combine(programRoot, ".tmp")));
            Assert.DoesNotContain(report.Items, item => item.Path.Contains(".tmp-project", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(report.Items, item => PathsEqual(item.Path, Path.Combine(programRoot, "src", "App", "obj")));
            Assert.Contains(report.Items, item => PathsEqual(item.Path, Path.Combine(programRoot, "src", "Other", "bin")));
            Assert.Contains(report.Items, item => PathsEqual(item.Path, Path.Combine(programRoot, "src", "App", "bin", "Release")));
            Assert.DoesNotContain(report.Items, item => IsSameOrAncestor(item.Path, currentAppBase));
            Assert.DoesNotContain(report.Items, item => item.Path.Contains("artifacts", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(report.Items, item => item.Path.Contains("models", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(report.Items, item => item.Path.Contains("ProtectedProject", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(report.Items, item => item.Path.Contains("CustomerProject", StringComparison.OrdinalIgnoreCase));

            var result = service.Clean(request);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.FailedPaths));
            Assert.False(Directory.Exists(Path.Combine(programRoot, ".tmp")));
            Assert.True(File.Exists(Path.Combine(programRoot, ".tmp-project", "Projektdateien", "projekt.json")));
            Assert.False(Directory.Exists(Path.Combine(programRoot, "src", "App", "obj")));
            Assert.False(Directory.Exists(Path.Combine(programRoot, "src", "Other", "bin")));
            Assert.False(Directory.Exists(Path.Combine(programRoot, "src", "App", "bin", "Release")));
            Assert.True(File.Exists(Path.Combine(currentAppBase, "SewerStudio.exe")));
            Assert.True(File.Exists(Path.Combine(programRoot, "src", "App", "important.tmp")));
            Assert.True(File.Exists(Path.Combine(programRoot, "artifacts", "release", "bin", "keep.dll")));
            Assert.True(File.Exists(Path.Combine(programRoot, "sidecar", "models", "__pycache__", "keep.pyc")));
            Assert.True(File.Exists(Path.Combine(protectedProject, "project.json")));
            Assert.True(File.Exists(Path.Combine(protectedProject, "bin", "keep.dll")));
            Assert.True(File.Exists(Path.Combine(programRoot, "CustomerProject", "bin", "keep.dll")));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Clean_removes_only_old_sewerstudio_files_from_windows_temp()
    {
        var testRoot = NewTestRoot();
        var programRoot = Path.Combine(testRoot, "program");
        var systemTemp = Path.Combine(testRoot, "windows-temp");
        var currentAppBase = Path.Combine(programRoot, "runtime");
        var cutoff = DateTime.UtcNow.AddDays(-1);
        var old = cutoff.AddHours(-2);

        var oldKnown = Path.Combine(systemTemp, "sewer_live_123.png");
        var recentKnown = Path.Combine(systemTemp, "sewer_studio_det_456.png");
        var unknown = Path.Combine(systemTemp, "customer.tmp");
        var snapshot = Path.Combine(systemTemp, "SewerStudio_Snapshots", "snap_old.png");
        var oldSewerStudioCache = Path.Combine(systemTemp, "SewerStudio", "coding_defect_previews", "old.png");
        var recentSewerStudioCache = Path.Combine(systemTemp, "SewerStudio", "coding_defect_previews", "recent.png");
        var referencedEvidence = Path.Combine(systemTemp, "SewerStudio", "coding_ai_frames", "keep.png");
        var importBackup = Path.Combine(systemTemp, "sewerstudio_import_backup_20260101", "data.bin");

        try
        {
            Directory.CreateDirectory(programRoot);
            Directory.CreateDirectory(currentAppBase);
            WriteFile(oldKnown, 100);
            WriteFile(recentKnown, 100);
            WriteFile(unknown, 100);
            WriteFile(snapshot, 100);
            WriteFile(oldSewerStudioCache, 100);
            WriteFile(recentSewerStudioCache, 100);
            WriteFile(referencedEvidence, 100);
            WriteFile(importBackup, 100);

            File.SetLastWriteTimeUtc(oldKnown, old);
            File.SetLastWriteTimeUtc(unknown, old);
            File.SetLastWriteTimeUtc(snapshot, old);
            File.SetLastWriteTimeUtc(oldSewerStudioCache, old);
            File.SetLastWriteTimeUtc(referencedEvidence, old);
            File.SetLastWriteTimeUtc(importBackup, old);
            Directory.SetLastWriteTimeUtc(Path.GetDirectoryName(importBackup)!, old);

            var request = new ProgramCleanupRequest(
                programRoot,
                systemTemp,
                currentAppBase,
                TemporaryFileCutoffUtc: cutoff);
            var service = new ProgramCleanupService();

            var report = service.Analyze(request);

            Assert.Contains(report.Items, item => PathsEqual(item.Path, oldKnown));
            Assert.Contains(report.Items, item => PathsEqual(item.Path, oldSewerStudioCache));
            Assert.DoesNotContain(report.Items, item => PathsEqual(item.Path, recentKnown));
            Assert.DoesNotContain(report.Items, item => PathsEqual(item.Path, unknown));
            Assert.DoesNotContain(report.Items, item => PathsEqual(item.Path, snapshot));
            Assert.DoesNotContain(report.Items, item => PathsEqual(item.Path, recentSewerStudioCache));
            Assert.DoesNotContain(report.Items, item => PathsEqual(item.Path, referencedEvidence));
            Assert.DoesNotContain(report.Items, item => PathsEqual(item.Path, Path.GetDirectoryName(importBackup)!));

            var result = service.Clean(request);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.FailedPaths));
            Assert.False(File.Exists(oldKnown));
            Assert.False(File.Exists(oldSewerStudioCache));
            Assert.True(File.Exists(recentKnown));
            Assert.True(File.Exists(unknown));
            Assert.True(File.Exists(snapshot));
            Assert.True(File.Exists(recentSewerStudioCache));
            Assert.True(File.Exists(referencedEvidence));
            Assert.True(File.Exists(importBackup));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string NewTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "sewerstudio-cleanup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
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

    private static bool IsSameOrAncestor(string ancestor, string path)
    {
        var normalizedAncestor = Path.GetFullPath(ancestor).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(normalizedAncestor, normalizedPath, StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith(
                   normalizedAncestor + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }
}
