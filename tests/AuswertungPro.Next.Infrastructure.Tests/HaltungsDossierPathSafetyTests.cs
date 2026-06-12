using System.Reflection;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HaltungsDossierPathSafetyTests
{
    [Fact]
    public void ResolveMediaPath_RejectsTraversalOutsideProjectRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dossier_root_{Guid.NewGuid():N}");
        var outside = Path.Combine(Path.GetTempPath(), $"dossier_outside_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            var outsideFile = Path.Combine(outside, "film.mp4");
            File.WriteAllText(outsideFile, "x");

            var raw = Path.Combine("..", Path.GetFileName(outside), "film.mp4");
            var resolved = InvokeResolveMediaPath(raw, root);

            Assert.Null(resolved);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
            try { Directory.Delete(outside, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ResolveMediaPath_AllowsExistingProjectRelativeFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dossier_root_{Guid.NewGuid():N}");
        try
        {
            var mediaDir = Path.Combine(root, "Haltungen", "A");
            Directory.CreateDirectory(mediaDir);
            var mediaFile = Path.Combine(mediaDir, "film.mp4");
            File.WriteAllText(mediaFile, "x");

            var resolved = InvokeResolveMediaPath(Path.Combine("Haltungen", "A", "film.mp4"), root);

            Assert.Equal(Path.GetFullPath(mediaFile), resolved);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static string? InvokeResolveMediaPath(string raw, string projectRoot)
    {
        var method = typeof(HaltungsDossierPdfBuilder).GetMethod(
            "ResolveMediaPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return (string?)method!.Invoke(null, [raw, projectRoot]);
    }
}
